namespace Core.Domain.Common;

public interface ISoftDelete
{
    bool IsDelete { get; set; }
}
