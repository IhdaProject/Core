using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Core.Domain.Helpers;

public static class HttpClientHelpers
{
    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 100
    };

    private static readonly Lazy<HttpClient> SharedClient = new(() => new HttpClient(new CustomHttpClientHandler(SharedHandler), disposeHandler: false));

    public static IHttpClientFactory? HttpClientFactory { get; set; }

    public static HttpClient GetInstance(string? baseAddress = null)
    {
        var httpClient = HttpClientFactory != null 
            ? HttpClientFactory.CreateClient() 
            : SharedClient.Value;

        if (string.IsNullOrEmpty(baseAddress))
            return httpClient;

        // If a specific baseAddress is needed on an isolated client instance without disposing shared handler
        return new HttpClient(new CustomHttpClientHandler(SharedHandler), disposeHandler: false)
        {
            BaseAddress = new Uri(baseAddress)
        };
    }

    public static HttpClient GetInstanceWithBaseAuth(string username, string password, string? baseAddress = null)
    {
        var httpClient = new HttpClient(new CustomHttpClientHandler(SharedHandler), disposeHandler: false);
        if (!string.IsNullOrEmpty(baseAddress))
        {
            httpClient.BaseAddress = new Uri(baseAddress);
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

        return httpClient;
    }

    public static HttpClient GetInstanceWithJwtAuth(string token, string? baseAddress = null)
    {
        var httpClient = new HttpClient(new CustomHttpClientHandler(SharedHandler), disposeHandler: false);
        if (!string.IsNullOrEmpty(baseAddress))
        {
            httpClient.BaseAddress = new Uri(baseAddress);
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return httpClient;
    }

    public static async Task<TResult?> PostJsonAsync<TIn, TResult>(this HttpClient client, string url, TIn data)
    {
        HttpContent content = new StringContent(SerializerHelper.ToJsonString(data));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var result = await client.PostAsync(url, content);

        result.EnsureSuccessStatusCode();
        return await result.Content.ReadFromJsonAsync<TResult>();
    }

    public static async Task<TResult?> GetJsonAsync<TResult>(this HttpClient client, string url,
        params KeyValuePair<string, object>[]? queryParams)
    {
        var queryString = "";
        if (queryParams is not null && queryParams.Length != 0)
            queryString = "?" + string.Join("&", queryParams.Select(x => $"{x.Key}={x.Value}"));

        using var request = new HttpRequestMessage(HttpMethod.Get, url + queryString);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var result = await client.SendAsync(request);

        result.EnsureSuccessStatusCode();
        return await result.Content.ReadFromJsonAsync<TResult>();
    }
}
