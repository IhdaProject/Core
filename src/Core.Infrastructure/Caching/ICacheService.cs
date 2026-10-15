namespace Core.Infrastructure.Caching;

public interface ICacheService
{
    Task<T?> GetDataAsync<T>(string key);
    Task<(bool Success, T? Data)> TryGetDataAsync<T>(string key);
    bool TryGetData<T>(string key, out T? data);
    Task<string?> GetJsonAsync(string key);
    Task<(bool Success, string? Data)> TryGetJsonAsync(string key);
    bool TryGetJson(string key, out string? data);
    Task<bool> SetDataAsync<T>(string key, T data, TimeSpan expiry);
    Task<bool> SetJsonAsync(string key, string dataJson, TimeSpan expiry);
    Task<bool> HasKeyAsync(string key);
    Task<bool> RemoveAsync(string key);
}
