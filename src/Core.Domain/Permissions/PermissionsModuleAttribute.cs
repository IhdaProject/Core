namespace Core.Domain.Permissions;

/// <summary>
/// Mikroservisning ruxsatlar enum'ini (yoki sinfini) belgilash uchun atribut.
/// Ushbu atribut orqali servis nomi, modul nomi va servis kodi ko'rsatiladi.
/// </summary>
/// <example>
/// <code>
/// [PermissionsModule("QuranCourse", serviceCode: 60, moduleName: "Quran Training")]
/// public enum QuranPermissions
/// {
///     CreateTrainingCenter = 60100001,
///     ViewTrainingCenters = 60100002
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class PermissionsModuleAttribute : Attribute
{
    /// <summary>
    /// Servis nomi (masalan: "QuranCourse", "OrderService").
    /// </summary>
    public string ServiceName { get; }

    /// <summary>
    /// Servis kodi (masalan: 60, 10).
    /// </summary>
    public int? ServiceCode { get; }

    /// <summary>
    /// Modul yoki bo'lim nomi.
    /// </summary>
    public string? ModuleName { get; }

    /// <summary>
    /// <see cref="PermissionsModuleAttribute"/> konstruktori.
    /// </summary>
    /// <param name="serviceName">Servis nomi.</param>
    /// <param name="serviceCode">Servis prefiks kodi (masalan: 60).</param>
    /// <param name="moduleName">Modul nomi (ixtiyoriy).</param>
    public PermissionsModuleAttribute(string serviceName, int serviceCode = 0, string? moduleName = null)
    {
        ServiceName = serviceName;
        ServiceCode = serviceCode > 0 ? serviceCode : null;
        ModuleName = moduleName;
    }
}
