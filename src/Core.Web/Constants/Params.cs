namespace Core.Web.Constants;

public static class Params
{
    [Obsolete("Use IConfiguration or dependency injection instead of static DbConnectionString.")]
    public static string DbConnectionString { get; set; } = string.Empty;

    public static string WebDomainString { get; set; } = string.Empty;
}
