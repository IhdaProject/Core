using System.Text;
using RestSharp;

namespace Core.Domain.Helpers;

public static class RestClientHelpers
{
    private static RestClient GetInstance(string? baseAddress = null)
    {
        var options = string.IsNullOrEmpty(baseAddress) 
            ? new RestClientOptions() 
            : new RestClientOptions(baseAddress);
            
        return new RestClient(options);
    }

    public static RestClient GetInstanceWithBaseAuth(string username, string password, string? baseAddress = null)
    {
        var restClient = GetInstance(baseAddress);
        restClient.AddDefaultHeader("Authorization",
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));

        return restClient;
    }

    public static RestClient GetInstanceWithJwtAuth(string token, string? baseAddress = null)
    {
        var restClient = GetInstance(baseAddress);
        restClient.AddDefaultHeader("Authorization", $"Bearer {token}");
        return restClient;
    }

    public static async Task<TResult?> PostJsonAsync<TIn, TResult>(this RestClient client, string url, TIn data)
        where TIn : class
    {
        var request = new RestRequest(url, Method.Post);
        request.AddJsonBody(data!);
        var result = await client.ExecuteAsync(request);
        
        if (!result.IsSuccessful)
            throw new ApplicationException($"Request failed with status {result.StatusCode}: {result.ErrorMessage}");

        return SerializerHelper.FromJsonString<TResult>(result.Content);
    }

    public static async Task<TResult?> GetJsonAsync<TResult>(this RestClient client,
        string url,
        params KeyValuePair<string, object>[]? queryParams)
    {
        var queryString = "";
        if (queryParams is not null && queryParams.Length != 0)
            queryString = "?" + string.Join("&", queryParams.Select(x => $"{x.Key}={x.Value}"));

        var result = await client.GetAsync(new RestRequest(url + queryString));

        if (!result.IsSuccessful)
            throw new ApplicationException($"Request failed with status {result.StatusCode}: {result.ErrorMessage}");

        return SerializerHelper.FromJsonString<TResult>(result.Content);
    }
}
