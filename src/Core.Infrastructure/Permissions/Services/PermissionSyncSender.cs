using Core.Domain.Permissions;
using Core.Infrastructure.Permissions.Grpc;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Permissions.Services;

/// <summary>
/// Mikroservis ruxsatlarini (Permissions) yig'ib, ularni RabbitMQ yoki gRPC
/// orqali AuthApi ga yuboruvchi asosiy implementatsiya.
/// </summary>
public class PermissionSyncSender : IPermissionSyncSender
{
    private readonly IEnumerable<Type> _enumTypes;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PermissionSyncSender> _logger;

    /// <summary>
    /// <see cref="PermissionSyncSender"/> konstruktori.
    /// </summary>
    public PermissionSyncSender(
        IEnumerable<Type> enumTypes,
        IServiceProvider serviceProvider,
        ILogger<PermissionSyncSender> logger)
    {
        _enumTypes = enumTypes;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SyncViaRabbitMqAsync(CancellationToken cancellationToken = default)
    {
        var publishEndpoint = _serviceProvider.GetService<IPublishEndpoint>();
        if (publishEndpoint == null)
        {
            _logger.LogWarning("MassTransit IPublishEndpoint topilmadi. Ruxsatlarni RabbitMQ orqali yuborib bo'lmadi.");
            return;
        }

        var events = BuildEvents();
        foreach (var @event in events)
        {
            _logger.LogInformation(
                "RabbitMQ ga ruxsatlar yuborilmoqda: Servis: {ServiceName}, Soni: {Count}",
                @event.ServiceName,
                @event.Permissions.Count);

            await publishEndpoint.Publish(@event, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SyncViaGrpcAsync(CancellationToken cancellationToken = default)
    {
        var grpcClient = _serviceProvider.GetService<IPermissionSyncGrpcService>();
        if (grpcClient == null)
        {
            _logger.LogWarning("IPermissionSyncGrpcService mijoz topilmadi. Ruxsatlarni gRPC orqali yuborib bo'lmadi.");
            return;
        }

        var events = BuildEvents();
        foreach (var ev in events)
        {
            _logger.LogInformation(
                "gRPC orqali AuthApi ga ruxsatlar yuborilmoqda: Servis: {ServiceName}, Soni: {Count}",
                ev.ServiceName,
                ev.Permissions.Count);

            var request = new SyncPermissionsRequest
            {
                ServiceName = ev.ServiceName,
                ServiceCode = ev.ServiceCode,
                Permissions = ev.Permissions
            };

            var response = await grpcClient.SyncPermissionsAsync(request);
            if (response.Success)
            {
                _logger.LogInformation("gRPC orqali ruxsatlar muvaffaqiyatli saqlandi: {Message}", response.Message);
            }
            else
            {
                _logger.LogError("gRPC ruxsatlarni saqlashda xatolik: {Message}", response.Message);
            }
        }
    }

    private List<SyncPermissionsIntegrationEvent> BuildEvents()
    {
        var result = new List<SyncPermissionsIntegrationEvent>();

        foreach (var enumType in _enumTypes)
        {
            var permissions = PermissionDiscoveryHelper.DiscoverFromEnum(enumType);
            if (permissions.Count == 0)
                continue;

            var first = permissions[0];
            var serviceName = first.ServiceName ?? enumType.Name;

            result.Add(new SyncPermissionsIntegrationEvent
            {
                ServiceName = serviceName,
                ServiceCode = permissions.FirstOrDefault()?.Code / 1000000,
                Permissions = permissions
            });
        }

        return result;
    }
}
