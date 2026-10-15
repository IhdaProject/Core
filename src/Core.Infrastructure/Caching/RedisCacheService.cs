using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Core.Infrastructure.Caching;

public class RedisCacheService(IDistributedCache distributedCache) : ICacheService
{
    public async Task<T?> GetDataAsync<T>(string key)
    {
        var json = await distributedCache.GetStringAsync(key);
        if (string.IsNullOrEmpty(json)) return default;

        return JsonSerializer.Deserialize<T>(json);
    }

    public async Task<(bool Success, T? Data)> TryGetDataAsync<T>(string key)
    {
        var json = await distributedCache.GetStringAsync(key);
        if (string.IsNullOrEmpty(json))
            return (false, default);

        try
        {
            var data = JsonSerializer.Deserialize<T>(json);
            return (data != null, data);
        }
        catch
        {
            return (false, default);
        }
    }

    public bool TryGetData<T>(string key, out T? data)
    {
        var task = TryGetDataAsync<T>(key);
        var result = task.GetAwaiter().GetResult();
        data = result.Data;
        return result.Success;
    }

    public async Task<string?> GetJsonAsync(string key)
    {
        return await distributedCache.GetStringAsync(key);
    }

    public async Task<(bool Success, string? Data)> TryGetJsonAsync(string key)
    {
        var data = await distributedCache.GetStringAsync(key);
        return (!string.IsNullOrEmpty(data), data);
    }

    public bool TryGetJson(string key, out string? data)
    {
        var task = TryGetJsonAsync(key);
        var result = task.GetAwaiter().GetResult();
        data = result.Data;
        return result.Success;
    }

    public async Task<bool> SetDataAsync<T>(string key, T data, TimeSpan expiry)
    {
        var json = JsonSerializer.Serialize(data);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry
        };
        await distributedCache.SetStringAsync(key, json, options);
        return true;
    }

    public async Task<bool> SetJsonAsync(string key, string dataJson, TimeSpan expiry)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry
        };
        await distributedCache.SetStringAsync(key, dataJson, options);
        return true;
    }

    public async Task<bool> HasKeyAsync(string key)
    {
        var data = await distributedCache.GetAsync(key);
        return data != null && data.Length > 0;
    }

    public async Task<bool> RemoveAsync(string key)
    {
        await distributedCache.RemoveAsync(key);
        return true;
    }
}
