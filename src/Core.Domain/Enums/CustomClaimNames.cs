namespace Core.Domain.Enums;

/// <summary>
/// JWT token va HttpContext da ishlatiladigan maxsus claim nomlari.
/// </summary>
public abstract class CustomClaimNames
{
    /// <summary>Foydalanuvchi unikal identifikatori.</summary>
    public const string UserId = "user-id";

    /// <summary>Foydalanuvchi tashkiliy tuzilmalari.</summary>
    public const string Structures = "structures";

    /// <summary>Ko'p ijarachilik (Multi-Tenancy) ijarachi identifikatori.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>Sessiya identifikatori (sessiyani bekor qilish va logout uchun).</summary>
    public const string SessionId = "session_id";

    /// <summary>Xavfsizlik tamg'asi (parol o'zgarganda barcha tokenlarni bekor qilish uchun).</summary>
    public const string SecurityStamp = "security_stamp";

    /// <summary>Foydalanuvchi ruxsatlari ro'yxati (RBAC).</summary>
    public const string Permissions = "permissions";
}
