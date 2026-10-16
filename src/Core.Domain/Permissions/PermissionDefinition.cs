using ProtoBuf;

namespace Core.Domain.Permissions;

/// <summary>
/// Yagona ruxsat (Permission) ma'lumotlari modeli.
/// gRPC va RabbitMQ orqali AuthApi ga uzatish uchun moslashtirilgan.
/// </summary>
[ProtoContract]
public class PermissionDefinition
{
    /// <summary>
    /// Ruxsatning unikal raqamli kodi (masalan: 60100001).
    /// </summary>
    [ProtoMember(1)]
    public int Code { get; set; }

    /// <summary>
    /// Ruxsatning dasturiy nomi (masalan: "CreateTrainingCenter").
    /// </summary>
    [ProtoMember(2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Ruxsatning inson tushunadigan tavsifi yoki izohi (Admin panelda ko'rsatish uchun).
    /// </summary>
    [ProtoMember(3)]
    public string? Description { get; set; }

    /// <summary>
    /// Ruxsat tegishli bo'lgan modul yoki guruh nomi (masalan: "Training Center", "Orders").
    /// </summary>
    [ProtoMember(4)]
    public string? ModuleName { get; set; }

    /// <summary>
    /// Servis nomi (masalan: "QuranCourse").
    /// </summary>
    [ProtoMember(5)]
    public string? ServiceName { get; set; }

    /// <summary>
    /// Bo'sh konstruktor.
    /// </summary>
    public PermissionDefinition() { }

    /// <summary>
    /// Parametrli konstruktor.
    /// </summary>
    public PermissionDefinition(int code, string name, string? description = null, string? moduleName = null, string? serviceName = null)
    {
        Code = code;
        Name = name;
        Description = description;
        ModuleName = moduleName;
        ServiceName = serviceName;
    }
}
