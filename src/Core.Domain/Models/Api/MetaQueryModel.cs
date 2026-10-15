using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Domain.Models.Api;

public class MetaQueryModel
{
    private string? _filteringExpressionsJson;
    private string? _sortingExpressionsJson;

    [JsonIgnore, NotMapped]
    public List<MetaQueryFilterModel>? FilteringExpressions { get; set; }

    public string? FilteringExpressionsJson
    {
        get => _filteringExpressionsJson;
        set
        {
            _filteringExpressionsJson = value;
            if (string.IsNullOrWhiteSpace(value))
            {
                FilteringExpressions = null;
                return;
            }

            try
            {
                FilteringExpressions = JsonSerializer.Deserialize<List<MetaQueryFilterModel>>(value);
            }
            catch
            {
                FilteringExpressions = null;
            }
        }
    }

    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 10;

    [JsonIgnore, NotMapped]
    public List<MetaQuerySortModel>? SortingExpressions { get; set; }

    public string? SortingExpressionsJson
    {
        get => _sortingExpressionsJson;
        set
        {
            _sortingExpressionsJson = value;
            if (string.IsNullOrWhiteSpace(value))
            {
                SortingExpressions = null;
                return;
            }

            try
            {
                SortingExpressions = JsonSerializer.Deserialize<List<MetaQuerySortModel>>(value);
            }
            catch
            {
                SortingExpressions = null;
            }
        }
    }
}
