using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Core.Domain.Helpers;

public static class HttpClientHelpers
{
    private static HttpClient GetInstance(string? baseAddress = null)
    {
        var httpClient = new HttpClient(new CustomHttpClientHandler());
        if (!string.IsNullOrEmpty(baseAddress))
        {
            httpClient.BaseAddress = new Uri(baseAddress);
        }
        return httpClient;
    }

    public static HttpClient GetInstanceWithBaseAuth(string username, string password, string? baseAddress = null)
    {
        var httpClient = GetInstance(baseAddress);
        httpClient.DefaultRequestHeaders.Add("Authorization",
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

        return httpClient;
    }

    public static HttpClient GetInstanceWithJwtAuth(string token, string? baseAddress = null)
    {
        var httpClient = GetInstance(baseAddress);
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
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

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var result = await client.GetAsync(url + queryString);

        result.EnsureSuccessStatusCode();
        return await result.Content.ReadFromJsonAsync<TResult>();
    }
}
