using Microsoft.Extensions.Caching.Memory;

namespace Core.Infrastructure.Caching;

public class MemoryCacheService(IMemoryCache memoryCache) : ICacheService
{
    public Task<T?> GetDataAsync<T>(string key)
    {
        if (memoryCache.TryGetValue(key, out T? data))
            return Task.FromResult(data);

        return Task.FromResult<T?>(default);
    }

    public Task<(bool Success, T? Data)> TryGetDataAsync<T>(string key)
    {
        var success = memoryCache.TryGetValue(key, out T? data);
        return Task.FromResult((success, data));
    }

    public bool TryGetData<T>(string key, out T? data)
    {
        return memoryCache.TryGetValue(key, out data);
    }

    public Task<string?> GetJsonAsync(string key)
    {
        if (memoryCache.TryGetValue(key, out string? data))
            return Task.FromResult<string?>(data);

        return Task.FromResult<string?>(null);
    }

    public Task<(bool Success, string? Data)> TryGetJsonAsync(string key)
    {
        var success = memoryCache.TryGetValue(key, out string? data);
        return Task.FromResult((success, data));
    }

    public bool TryGetJson(string key, out string? data)
    {
        return memoryCache.TryGetValue(key, out data);
    }

    public Task<bool> SetDataAsync<T>(string key, T data, TimeSpan expiry)
    {
        memoryCache.Set(key, data, expiry);
        return Task.FromResult(true);
    }

    public Task<bool> SetJsonAsync(string key, string dataJson, TimeSpan expiry)
    {
        memoryCache.Set(key, dataJson, expiry);
        return Task.FromResult(true);
    }

    public Task<bool> HasKeyAsync(string key)
    {
        return Task.FromResult(memoryCache.TryGetValue(key, out _));
    }

    public Task<bool> RemoveAsync(string key)
    {
        memoryCache.Remove(key);
        return Task.FromResult(true);
    }
}
