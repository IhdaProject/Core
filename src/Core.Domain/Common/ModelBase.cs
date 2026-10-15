using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Domain.Common;

public abstract class ModelBase<TId>
{
    [Column("id")] 
    public TId Id { get; set; } = default!;

    [Column("is_delete")] 
    public bool IsDelete { get; set; } = false;
}
