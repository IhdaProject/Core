namespace Core.Domain.Models.Api;

public class MetaQuerySortModel
{
    public string SortFieldName { get; set; } = string.Empty;
    public string Direction { get; set; } = "asc";
}
