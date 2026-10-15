using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Domain.Common;

public abstract class AuditableModelBase<TId, TUserId> : ModelBase<TId>, IAuditable<TUserId>
{
    [Column("created_at")] 
    public DateTime? CreatedAt { get; set; }

    [Column("created_by")] 
    public TUserId CreatedBy { get; set; } = default!;

    [Column("updated_at")] 
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")] 
    public TUserId UpdatedBy { get; set; } = default!;
}

public abstract class AuditableModelBase<TId> : AuditableModelBase<TId, long>
{
}
