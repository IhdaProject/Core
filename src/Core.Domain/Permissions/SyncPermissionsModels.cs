using ProtoBuf;

namespace Core.Domain.Permissions;

/// <summary>
/// gRPC orqali ruxsatlarni sinxronlashtirish so'rovi.
/// </summary>
[ProtoContract]
public class SyncPermissionsRequest
{
    /// <summary>
    /// Servis nomi (masalan: "QuranCourse", "OrderService").
    /// </summary>
    [ProtoMember(1)]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Servis prefiks kodi (masalan: 60).
    /// </summary>
    [ProtoMember(2)]
    public int? ServiceCode { get; set; }

    /// <summary>
    /// Servisdagi barcha ruxsatlar ro'yxati.
    /// </summary>
    [ProtoMember(3)]
    public List<PermissionDefinition> Permissions { get; set; } = [];
}

/// <summary>
/// gRPC orqali ruxsatlarni sinxronlashtirish javobi.
/// </summary>
[ProtoContract]
public class SyncPermissionsResponse
{
    /// <summary>
    /// Amaliyot muvaffaqiyatli yakunlanganligi.
    /// </summary>
    [ProtoMember(1)]
    public bool Success { get; set; }

    /// <summary>
    /// Saqlangan yoki yangilangan ruxsatlar soni.
    /// </summary>
    [ProtoMember(2)]
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Qo'shimcha xabar.
    /// </summary>
    [ProtoMember(3)]
    public string? Message { get; set; }
}
