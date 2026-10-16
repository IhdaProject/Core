using Core.Domain.Events;

namespace Core.Domain.Permissions;

/// <summary>
/// Mikroservis ishga tushganda o'zidagi barcha ruxsatlar (Permissions)
/// ro'yxatini AuthApi ga yuborish uchun asinxron hodisa (RabbitMQ xabari).
/// </summary>
public record SyncPermissionsIntegrationEvent : IntegrationEvent
{
    /// <summary>
    /// Ruxsatlarni yuborayotgan servis nomi (masalan: "QuranCourse", "OrderService").
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Servis prefiks kodi (masalan: 60).
    /// </summary>
    public int? ServiceCode { get; init; }

    /// <summary>
    /// Servisdagi barcha mavjud ruxsatlar to'plami.
    /// </summary>
    public List<PermissionDefinition> Permissions { get; init; } = [];
}
