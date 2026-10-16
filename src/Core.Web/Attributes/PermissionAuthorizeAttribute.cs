using Core.Domain.Enums;
using Core.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Core.Web.Attributes;

/// <summary>
/// Endpoint yoki Kontrollerga kirish huquqini (RBAC Permission) tekshiruvchi atribut.
/// Quyidagi barcha turlarni qabul qila oladi:
/// 1. Markaziy <see cref="UserPermissions"/> enumi (orqaga to'liq moslik uchun).
/// 2. Har bir mikroservisning o'ziga tegishli ixtiyoriy lokal enumi (masalan: <c>QuranPermissions.CreateTrainingCenter</c>).
/// 3. Raqamli ruxsat kodlari (masalan: <c>60100001</c>).
/// </summary>
/// <example>
/// <code>
/// // Servisning o'z lokal enumi orqali:
/// [PermissionAuthorize(QuranPermissions.CreateTrainingCenter)]
/// public async Task&lt;IActionResult&gt; Create() { ... }
/// 
/// // Bir nechta ruxsatlarni tekshirish:
/// [PermissionAuthorize(QuranPermissions.CreateTrainingCenter, QuranPermissions.UpdateTrainingCenter)]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAuthorizeAttribute : TypeFilterAttribute
{
    /// <summary>
    /// Standart markaziy UserPermissions enum qiymatlari uchun konstruktor.
    /// </summary>
    /// <param name="permissions">Talab qilinadigan ruxsatlar.</param>
    public PermissionAuthorizeAttribute(params UserPermissions[] permissions)
        : base(typeof(PermissionRequirementFilter))
    {
        Arguments = [ permissions.Cast<int>().ToArray() ];
    }

    /// <summary>
    /// Raqamli ruxsat kodlari uchun konstruktor.
    /// </summary>
    /// <param name="permissionCodes">Ruxsat kodlari massivi.</param>
    public PermissionAuthorizeAttribute(params int[] permissionCodes)
        : base(typeof(PermissionRequirementFilter))
    {
        Arguments = [ permissionCodes ];
    }

    /// <summary>
    /// Istalgan mikroservisning o'ziga tegishli lokal enum qiymatlari uchun universal konstruktor.
    /// Har qanday enum qiymatini uning sonli qiymatiga (int) avtomatik o'giradi.
    /// </summary>
    /// <param name="permissions">Lokal enum yoki int qiymatlar.</param>
    public PermissionAuthorizeAttribute(params object[] permissions)
        : base(typeof(PermissionRequirementFilter))
    {
        Arguments = [ ExtractCodes(permissions) ];
    }

    private static int[] ExtractCodes(object[] permissions)
    {
        if (permissions == null || permissions.Length == 0)
            return [];

        var list = new List<int>(permissions.Length);
        foreach (var p in permissions)
        {
            if (p is Enum e)
            {
                list.Add(Convert.ToInt32(e));
            }
            else if (p is int i)
            {
                list.Add(i);
            }
            else if (p is IConvertible c)
            {
                list.Add(c.ToInt32(null));
            }
        }
        return list.ToArray();
    }
}
