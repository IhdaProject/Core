using System.ComponentModel.DataAnnotations.Schema;
using Core.Domain.Common;

namespace Core.Domain.Models.Auth;

[Table("structures", Schema = "auth")]
public class Structure : AuditableModelBase<long>
{
    [Column("name")] 
    public string Name { get; set; } = string.Empty;

    public virtual ICollection<StructurePermission> StructurePermissions { get; set; } = new List<StructurePermission>();

    /// <summary>
    /// 1 default, 2 superAdmin
    /// </summary>
    [Column("type")] 
    public int Type { get; set; }
}
