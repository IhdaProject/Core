using System.Collections.Concurrent;

namespace Core.Web.Constants;

public static class StaticCache
{
    public static ConcurrentDictionary<long, List<int>> Permissions { get; set; } = new();
    public static string SymmetricKey { get; set; } = string.Empty;
}
