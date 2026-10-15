using System.ComponentModel.DataAnnotations.Schema;
using Core.Domain.Common;

namespace Core.Domain.Models.Auth;

[Table("structure_permissions", Schema = "auth")]
public class StructurePermission : AuditableModelBase<long>
{
    [Column("structure_id"), ForeignKey(nameof(Structure))]
    public long StructureId { get; set; }

    public virtual Structure Structure { get; set; } = null!;

    [Column("permission_id"), ForeignKey(nameof(Permission))]
    public long PermissionId { get; set; }

    public virtual Permission Permission { get; set; } = null!;
}
