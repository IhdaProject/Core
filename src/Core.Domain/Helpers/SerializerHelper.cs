using System.Text.Json;

namespace Core.Domain.Helpers;

public static class SerializerHelper
{
    public static string ToJsonString(object? data)
    {
        return JsonSerializer.Serialize(data);
    }

    public static T? FromJsonString<T>(string? content)
    {
        if (string.IsNullOrEmpty(content)) return default;
        return JsonSerializer.Deserialize<T>(content);
    }
}
