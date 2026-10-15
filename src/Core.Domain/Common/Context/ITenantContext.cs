namespace Core.Domain.Common.Context;

/// <summary>
/// Ijarachi qamrovi (Tenant Scope).
/// Alohida bir ijarachiga tegishli yoki butun tizim bo'yicha global ekanligini belgilaydi.
/// </summary>
public enum TenantScope
{
    /// <summary>
    /// Muayyan bir ijarachiga (kompaniya, filial yoki tashkilotga) tegishli bo'lgan ma'lumotlar.
    /// </summary>
    Tenant = 1,

    /// <summary>
    /// Butun tizimga tegishli umumiy / global ma'lumotlar yoki super-admin qamrovi.
    /// </summary>
    Global = 2
}

/// <summary>
/// Ko'p ijarachilik (Multi-Tenancy) kontekst interfeysi.
/// Kiruvchi HTTP so'rov, RabbitMQ xabarlari yoki fon vazifalarida joriy ijarachi ma'lumotlarini taqdim etadi.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Joriy so'rovga tegishli ijarachining unikal identifikatori (ID).
    /// Agar so'rov global yoki aniqlanmagan bo'lsa null bo'ladi.
    /// </summary>
    long? TenantId { get; }

    /// <summary>
    /// Joriy ijarachining qisqa kodi yoki slug nomi (masalan: "tashkent_branch", "company_a").
    /// </summary>
    string? TenantCode { get; }

    /// <summary>
    /// Joriy ijarachi qamrovi (<see cref="TenantScope.Tenant"/> yoki <see cref="TenantScope.Global"/>).
    /// </summary>
    TenantScope Scope { get; }

    /// <summary>
    /// Joriy so'rov global super-admin yoki barcha ijarachilarni qamrab oluvchi vazifa ekanligini bildiradi.
    /// </summary>
    bool IsCrossTenant => Scope == TenantScope.Global;

    /// <summary>
    /// EF Core Global Query Filter ayni vaqtda vaqtincha o'chirilganligini ko'rsatadi.
    /// </summary>
    bool IsTenantFilterDisabled { get; }

    /// <summary>
    /// Fon vazifalari (Hangfire/Cron) yoki integratsiyalarda ijarachini vaqtinchalik xavfsiz o'zgartiradi.
    /// Using bloki tugashi bilan avvalgi holat tiklanadi.
    /// </summary>
    /// <param name="tenantId">Yangi ijarachi identifikatori.</param>
    /// <param name="tenantCode">Yangi ijarachi kodi (ixtiyoriy).</param>
    /// <returns>Avvalgi kontekstni tiklovchi <see cref="IDisposable"/> tokeni.</returns>
    /// <example>
    /// <code>
    /// using (tenantContext.Change(targetTenantId))
    /// {
    ///     await orderService.ProcessTenantOrdersAsync();
    /// }
    /// </code>
    /// </example>
    IDisposable Change(long? tenantId, string? tenantCode = null);

    /// <summary>
    /// Butun tizim hisobotlari yoki fon tahlillari uchun ijarachi filtrini vaqtincha o'chirib qo'yadi.
    /// Using bloki tugashi bilan filtr yana faollashadi (Soft-delete filtri esa doim o'z kuchida qoladi).
    /// </summary>
    /// <returns>Filtrni qayta yoquvchi <see cref="IDisposable"/> tokeni.</returns>
    IDisposable DisableTenantFilter();
}

/// <summary>
/// Standart bo'sh (null-object) ijarachi konteksti.
/// Sinovlar yoki ijarachi tizimi mavjud bo'lmagan servislar uchun sukut bo'yicha xavfsiz ishlatiladi.
/// </summary>
public sealed class NullTenantContext : ITenantContext
{
    /// <summary>
    /// Yagona statik nusxa (Singleton).
    /// </summary>
    public static readonly NullTenantContext Instance = new();

    private NullTenantContext() { }

    /// <inheritdoc />
    public long? TenantId => null;

    /// <inheritdoc />
    public string? TenantCode => null;

    /// <inheritdoc />
    public TenantScope Scope => TenantScope.Global;

    /// <inheritdoc />
    public bool IsCrossTenant => true;

    /// <inheritdoc />
    public bool IsTenantFilterDisabled => true;

    /// <inheritdoc />
    public IDisposable Change(long? tenantId, string? tenantCode = null) => NoOpDisposable.Instance;

    /// <inheritdoc />
    public IDisposable DisableTenantFilter() => NoOpDisposable.Instance;

    private sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable Instance = new();
        public void Dispose() { }
    }
}
