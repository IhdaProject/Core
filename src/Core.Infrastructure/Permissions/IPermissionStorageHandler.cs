using Core.Domain.Permissions;

namespace Core.Infrastructure.Permissions;

/// <summary>
/// AuthApi tomonida mikroservislardan kelgan ruxsatlarni (RabbitMQ yoki gRPC orqali)
/// qabul qilib, o'zining ma'lumotlar bazasiga (Permissions jadvaliga) saqlash
/// yoki yangilash uchun ishlatiladigan interfeys.
/// AuthApi ushbu interfeysni amalga oshiradi (implement qiladi).
/// </summary>
public interface IPermissionStorageHandler
{
    /// <summary>
    /// Mikroservisdan kelgan barcha ruxsatlar to'plamini bazaga saqlaydi yoki yangilaydi (Upsert).
    /// </summary>
    /// <param name="serviceName">Ruxsatlarni yuborgan mikroservis nomi.</param>
    /// <param name="serviceCode">Mikroservis kodi (masalan: 60).</param>
    /// <param name="permissions">Kelgan ruxsatlar ro'yxati.</param>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    /// <returns>Qayta ishlangan ruxsatlar soni.</returns>
    Task<int> HandlePermissionsReceivedAsync(
        string serviceName,
        int? serviceCode,
        IReadOnlyList<PermissionDefinition> permissions,
        CancellationToken cancellationToken = default);
}
