using Core.Domain.Common.Context;
using Microsoft.Extensions.Caching.Hybrid;

namespace Core.Infrastructure.Caching.Hybrid;

/// <summary>
/// .NET 10 <see cref="HybridCache"/> asosidagi xizmat implementatsiyasi.
/// L1 In-Memory + L2 Redis darajalarini birlashtiradi, Lock-on-Read orqali Cache Stampede himoyasini beradi,
/// hamda agar loyihada ko'p ijarachilik (Multi-Tenancy) mavjud bo'lsa, kesh kalitlarini avtomatik
/// ijarachi prefiksi bilan xavfsiz izolyatsiya qiladi.
/// </summary>
public class HybridCacheService : IHybridCacheService
{
    private readonly HybridCache _hybridCache;
    private readonly ITenantContext? _tenantContext;

    /// <summary>
    /// <see cref="HybridCacheService"/> konstruktori.
    /// </summary>
    /// <param name="hybridCache">.NET 10 HybridCache instansiyasi.</param>
    /// <param name="tenantContext">Ijarachi konteksti (ixtiyoriy, mavjud bo'lsa kalitlar avtomatik izolyatsiya qilinadi).</param>
    public HybridCacheService(HybridCache hybridCache, ITenantContext? tenantContext = null)
    {
        _hybridCache = hybridCache;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan? localTtl = null,
        TimeSpan? remoteTtl = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedKey = BuildKey(key);
        var resolvedTags = tags?.Select(BuildTag).ToList();

        var options = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = localTtl ?? TimeSpan.FromSeconds(30),
            Expiration = remoteTtl ?? TimeSpan.FromMinutes(5)
        };

        return await _hybridCache.GetOrCreateAsync(resolvedKey, factory, options, resolvedTags, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? localTtl = null,
        TimeSpan? remoteTtl = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedKey = BuildKey(key);
        var resolvedTags = tags?.Select(BuildTag).ToList();

        var options = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = localTtl ?? TimeSpan.FromSeconds(30),
            Expiration = remoteTtl ?? TimeSpan.FromMinutes(5)
        };

        await _hybridCache.SetAsync(resolvedKey, value, options, resolvedTags, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var resolvedKey = BuildKey(key);
        await _hybridCache.RemoveAsync(resolvedKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var resolvedTag = BuildTag(tag);
        await _hybridCache.RemoveByTagAsync(resolvedTag, cancellationToken);
    }

    private string BuildTag(string tag)
    {
        var prefix = GetTenantPrefix();
        return string.IsNullOrEmpty(prefix) ? tag : $"{prefix}:{tag}";
    }

    private string BuildKey(string key)
    {
        var prefix = GetTenantPrefix();
        return string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";
    }

    private string GetTenantPrefix()
    {
        if (_tenantContext == null)
            return string.Empty;

        if (_tenantContext.IsCrossTenant)
            return $"scope:{_tenantContext.Scope.ToString().ToLowerInvariant()}";

        return _tenantContext.TenantId.HasValue ? $"tenant:{_tenantContext.TenantId.Value}" : string.Empty;
    }
}
