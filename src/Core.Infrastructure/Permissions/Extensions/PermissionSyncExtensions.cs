using Core.Domain.Permissions;
using Core.Infrastructure.Permissions.Consumers;
using Core.Infrastructure.Permissions.Grpc;
using Core.Infrastructure.Permissions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Permissions.Extensions;

/// <summary>
/// Ruxsatlarni (Permissions) avtomatik sinxronlashtirish va qabul qilish uchun DI kengaytmalari.
/// </summary>
public static class PermissionSyncExtensions
{
    /// <summary>
    /// Mikroservisning o'z lokal ruxsatlar enum'ini ro'yxatdan o'tkazadi va
    /// servis ishga tushganda avtomatik tarzda AuthApi ga (RabbitMQ yoki gRPC orqali)
    /// yuborilishini ta'minlaydi.
    /// </summary>
    /// <typeparam name="TEnum">Mikroservisning ruxsatlar enum turi (masalan: QuranPermissions).</typeparam>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="mode">Yuborish usuli (sukut bo'yicha RabbitMq).</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// // Mikroservis Program.cs da:
    /// builder.Services.AddCorePermissionAutoSync&lt;QuranPermissions&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCorePermissionAutoSync<TEnum>(
        this IServiceCollection services,
        PermissionSyncMode mode = PermissionSyncMode.RabbitMq)
        where TEnum : struct, Enum
    {
        return services.AddCorePermissionAutoSync(mode, typeof(TEnum));
    }

    /// <summary>
    /// Bir nechta lokal ruxsatlar enum turlarini avtomatik sinxronlashtirish uchun ro'yxatdan o'tkazadi.
    /// </summary>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="mode">Yuborish usuli.</param>
    /// <param name="enumTypes">Enum turlari ro'yxati.</param>
    /// <returns>Servislar to'plami.</returns>
    public static IServiceCollection AddCorePermissionAutoSync(
        this IServiceCollection services,
        PermissionSyncMode mode = PermissionSyncMode.RabbitMq,
        params Type[] enumTypes)
    {
        services.TryAddSingleton<IPermissionSyncSender>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<PermissionSyncSender>>();
            return new PermissionSyncSender(enumTypes, sp, logger);
        });

        services.AddHostedService(sp =>
        {
            var sender = sp.GetRequiredService<IPermissionSyncSender>();
            var logger = sp.GetRequiredService<ILogger<PermissionAutoSyncHostedService>>();
            return new PermissionAutoSyncHostedService(sender, mode, logger);
        });

        return services;
    }

    /// <summary>
    /// AuthApi tomonida barcha mikroservislardan kelgan ruxsatlarni qabul qilib,
    /// ma'lumotlar bazasiga saqlovchi receiver tizimini ro'yxatdan o'tkazadi.
    /// Ham RabbitMQ Consumer hamda gRPC server servisini biriktiradi.
    /// </summary>
    /// <typeparam name="TStorageHandler">AuthApi da ruxsatlarni DB ga yozuvchi xizmat implementatsiyasi.</typeparam>
    /// <param name="services">DI servislar to'plami.</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// // AuthApi Program.cs da:
    /// builder.Services.AddCorePermissionReceiver&lt;AuthPermissionDatabaseHandler&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCorePermissionReceiver<TStorageHandler>(
        this IServiceCollection services)
        where TStorageHandler : class, IPermissionStorageHandler
    {
        services.TryAddScoped<IPermissionStorageHandler, TStorageHandler>();
        services.TryAddScoped<PermissionSyncRabbitMqConsumer>();
        services.TryAddScoped<PermissionSyncGrpcService>();

        return services;
    }
}
