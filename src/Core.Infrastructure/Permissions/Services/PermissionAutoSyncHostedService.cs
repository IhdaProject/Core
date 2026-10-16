using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Permissions.Services;

/// <summary>
/// Mikroservis ishga tushganida (Application Startup) fonda avtomatik ravishda
/// ruxsatlarni AuthApi ga yuboruvchi orqa fon xizmati (<see cref="IHostedService"/>).
/// Dasturchi har safar qo'lda kod yozishi shart emas, servis start bo'lishi bilan
/// o'z ruxsatlarini ro'yxatdan o'tkazadi.
/// </summary>
public class PermissionAutoSyncHostedService : BackgroundService
{
    private readonly IPermissionSyncSender _sender;
    private readonly PermissionSyncMode _mode;
    private readonly ILogger<PermissionAutoSyncHostedService> _logger;

    /// <summary>
    /// <see cref="PermissionAutoSyncHostedService"/> konstruktori.
    /// </summary>
    /// <param name="sender">Ruxsatlarni yuboruvchi xizmat.</param>
    /// <param name="mode">Sinxronlashtirish usuli (RabbitMQ yoki gRPC).</param>
    /// <param name="logger">Tizim logeri.</param>
    public PermissionAutoSyncHostedService(
        IPermissionSyncSender sender,
        PermissionSyncMode mode,
        ILogger<PermissionAutoSyncHostedService> logger)
    {
        _sender = sender;
        _mode = mode;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // RabbitMQ yoki tarmoq to'liq ulanib olishi uchun dastlab 2 soniya kutamiz
            await Task.Delay(2000, stoppingToken);

            if (_mode is PermissionSyncMode.RabbitMq or PermissionSyncMode.Both)
            {
                _logger.LogInformation("Avtomatik ruxsatlar sinxronizatsiyasi boshlandi (RabbitMQ)...");
                await _sender.SyncViaRabbitMqAsync(stoppingToken);
            }

            if (_mode is PermissionSyncMode.Grpc or PermissionSyncMode.Both)
            {
                _logger.LogInformation("Avtomatik ruxsatlar sinxronizatsiyasi boshlandi (gRPC)...");
                await _sender.SyncViaGrpcAsync(stoppingToken);
            }

            _logger.LogInformation("Avtomatik ruxsatlar sinxronizatsiyasi yakunlandi.");
        }
        catch (OperationCanceledException)
        {
            // Ilova to'xtatildi
        }
        catch (Exception ex)
        {
            // Xatolik ilovani to'xtatib qo'ymasligi kerak
            _logger.LogError(ex, "Avtomatik ruxsatlarni sinxronlashda xatolik yuz berdi");
        }
    }
}
