using Core.Domain.Permissions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Permissions.Consumers;

/// <summary>
/// AuthApi tomonida mikroservislar ishga tushganda RabbitMQ orqali yuborgan
/// <see cref="SyncPermissionsIntegrationEvent"/> xabarlarini qabul qilib oluvchi iste'molchi (Consumer).
/// Kelgan ruxsatlarni <see cref="IPermissionStorageHandler"/> ga uzatadi.
/// </summary>
public class PermissionSyncRabbitMqConsumer : IConsumer<SyncPermissionsIntegrationEvent>
{
    private readonly IPermissionStorageHandler _storageHandler;
    private readonly ILogger<PermissionSyncRabbitMqConsumer> _logger;

    /// <summary>
    /// <see cref="PermissionSyncRabbitMqConsumer"/> konstruktori.
    /// </summary>
    public PermissionSyncRabbitMqConsumer(
        IPermissionStorageHandler storageHandler,
        ILogger<PermissionSyncRabbitMqConsumer> logger)
    {
        _storageHandler = storageHandler;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<SyncPermissionsIntegrationEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "RabbitMQ orqali yangi ruxsatlar to'plami qabul qilindi. Servis: {ServiceName}, Ruxsatlar soni: {Count}",
            message.ServiceName,
            message.Permissions.Count);

        var processed = await _storageHandler.HandlePermissionsReceivedAsync(
            message.ServiceName,
            message.ServiceCode,
            message.Permissions,
            context.CancellationToken);

        _logger.LogInformation(
            "{ServiceName} servisi uchun {Processed} ta ruxsat muvaffaqiyatli saqlandi/yangilandi.",
            message.ServiceName,
            processed);
    }
}
