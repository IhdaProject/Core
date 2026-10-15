namespace Core.Domain.Models.Api;

public class MetaQueryFilterModel
{
    public string PropertyName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
