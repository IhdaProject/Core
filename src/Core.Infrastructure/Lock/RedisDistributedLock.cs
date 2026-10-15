using Core.Domain.Common.Lock;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Core.Infrastructure.Lock;

/// <summary>
/// Redis asosidagi taqsimlangan qulflash (Distributed Lock) mexanizmi.
/// Redis serverining atomik <c>LockTakeAsync</c> va <c>LockReleaseAsync</c> buyruqlari yordamida
/// poyga holatlari (race condition) va parallel to'qnashuvlarning oldini oladi.
/// Shuningdek, uzoq davom etadigan operatsiyalarda qulf ochilib ketmasligi uchun
/// avtomatik uzaytirib turuvchi fon qo'riqchisi (Watchdog/Heartbeat) bilan ta'minlangan.
/// </summary>
public class RedisDistributedLock : IDistributedLock
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisDistributedLock> _logger;

    /// <summary>
    /// <see cref="RedisDistributedLock"/> konstruktori.
    /// </summary>
    /// <param name="redis">Redis ulanish obyekti (<see cref="IConnectionMultiplexer"/>).</param>
    /// <param name="logger">Tizim logeri.</param>
    public RedisDistributedLock(
        IConnectionMultiplexer redis,
        ILogger<RedisDistributedLock> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable?> TryAcquireLockAsync(
        string resourceKey,
        TimeSpan timeout,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        var lockValue = Guid.NewGuid().ToString("N");
        var lockKey = $"lock:{resourceKey}";
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (cancellationToken.IsCancellationRequested)
                return null;

            var acquired = await _database.LockTakeAsync(lockKey, lockValue, expiry);
            if (acquired)
            {
                _logger.LogDebug("Taqsimlangan qulf muvaffaqiyatli olindi: {LockKey}", lockKey);
                return new LockReleaser(_database, lockKey, lockValue, expiry, _logger);
            }

            try
            {
                // Parallel so'rovlarning bir vaqtda Redis'ga urilishini (Lock Stampede)
                // oldini olish uchun 35-65ms oralig'ida tasodifiy kechikish (jitter) beriladi.
                var jitterMs = Random.Shared.Next(35, 65);
                await Task.Delay(jitterMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        _logger.LogWarning("Taqsimlangan qulfni {Timeout} ichida olib bo'lmadi: {LockKey}", timeout, lockKey);
        return null;
    }

    /// <inheritdoc />
    public async Task<bool> IsLockedAsync(string resourceKey, CancellationToken cancellationToken = default)
    {
        var lockKey = $"lock:{resourceKey}";
        var val = await _database.LockQueryAsync(lockKey);
        return !val.IsNull;
    }

    /// <summary>
    /// Qulfni o'z vaqtida xavfsiz bo'shatuvchi va uzoq operatsiyalarda
    /// uning muddatini fonda uzaytirib boruvchi (Watchdog) ichki yordamchi sinf.
    /// </summary>
    private sealed class LockReleaser : IAsyncDisposable
    {
        private readonly IDatabase _database;
        private readonly string _lockKey;
        private readonly string _lockValue;
        private readonly TimeSpan _expiry;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task? _renewalTask;
        private int _released;

        /// <summary>
        /// LockReleaser konstruktori va avtomatik uzaytirish vazifasini ishga tushirish.
        /// </summary>
        public LockReleaser(IDatabase database, string lockKey, string lockValue, TimeSpan expiry, ILogger logger)
        {
            _database = database;
            _lockKey = lockKey;
            _lockValue = lockValue;
            _expiry = expiry;
            _logger = logger;

            // Qulf muddatining yarmi vaqtida fonda uzaytirish siklini boshlaymiz (kamida 1 soniya)
            var renewalInterval = TimeSpan.FromMilliseconds(Math.Max(1000, expiry.TotalMilliseconds / 2));
            _renewalTask = StartAutoRenewalAsync(renewalInterval, _cts.Token);
        }

        /// <summary>
        /// PeriodicTimer yordamida qulf muddatini vaqti-vaqti bilan avtomatik uzaytiradi (Watchdog).
        /// </summary>
        private async Task StartAutoRenewalAsync(TimeSpan interval, CancellationToken ct)
        {
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    var extended = await _database.LockExtendAsync(_lockKey, _lockValue, _expiry);
                    if (!extended)
                    {
                        _logger.LogWarning("Qulf muddatini uzaytirib bo'lmadi: {LockKey}", _lockKey);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal tugash
            }
        }

        /// <summary>
        /// Using bloki yakunlanganda qulfni atomik tarzda bo'shatadi va watchdog'ni to'xtatadi.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _cts.Cancel();
                if (_renewalTask is not null)
                {
                    try { await _renewalTask; } catch { }
                }
                _cts.Dispose();

                var success = await _database.LockReleaseAsync(_lockKey, _lockValue);
                if (success)
                    _logger.LogDebug("Taqsimlangan qulf muvaffaqiyatli bo'shatildi: {LockKey}", _lockKey);
                else
                    _logger.LogWarning("Qulf muddati tugab ulgurgan yoki boshqa qiymat yozilgan: {LockKey}", _lockKey);
            }
        }
    }
}
