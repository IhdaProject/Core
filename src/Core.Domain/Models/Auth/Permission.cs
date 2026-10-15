using System.ComponentModel.DataAnnotations.Schema;
using Core.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Core.Domain.Models.Auth;

[Table("permissions", Schema = "auth"), Index(nameof(Code), IsUnique = true)]
public class Permission : ModelBase<long>
{
    [Column("name")] 
    public string Name { get; set; } = string.Empty;

    [Column("code")] 
    public int Code { get; set; }
}
