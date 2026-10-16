namespace Core.Infrastructure.Permissions.Services;

/// <summary>
/// Ruxsatlarni sinxronlashtirish rejimi (qaysi transport orqali yuborish).
/// </summary>
public enum PermissionSyncMode
{
    /// <summary>
    /// RabbitMQ orqali asinxron event yuborish (tavsiya etiladi).
    /// </summary>
    RabbitMq = 1,

    /// <summary>
    /// Code-First gRPC orqali to'g'ridan-to'g'ri AuthApi ga sinxron murojaat qilish.
    /// </summary>
    Grpc = 2,

    /// <summary>
    /// Ikkala kanal (RabbitMQ va gRPC) orqali yuborishga urinish.
    /// </summary>
    Both = 3
}

/// <summary>
/// Mikroservis ruxsatlarini AuthApi ga yuboruvchi xizmat interfeysi.
/// </summary>
public interface IPermissionSyncSender
{
    /// <summary>
    /// Ro'yxatdan o'tgan barcha enum turlaridagi ruxsatlarni RabbitMQ orqali yuboradi.
    /// </summary>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    Task SyncViaRabbitMqAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ro'yxatdan o'tgan barcha enum turlaridagi ruxsatlarni gRPC orqali AuthApi ga yuboradi.
    /// </summary>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    Task SyncViaGrpcAsync(CancellationToken cancellationToken = default);
}
