using Core.Domain.Permissions;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc;

namespace Core.Infrastructure.Permissions.Grpc;

/// <summary>
/// AuthApi tomonida mikroservislardan kelgan sinxron gRPC so'rovlarini qabul qiluvchi server xizmati.
/// Kelgan ruxsatlarni <see cref="IPermissionStorageHandler"/> ga uzatadi va natijani qaytaradi.
/// </summary>
public class PermissionSyncGrpcService : IPermissionSyncGrpcService
{
    private readonly IPermissionStorageHandler _storageHandler;
    private readonly ILogger<PermissionSyncGrpcService> _logger;

    /// <summary>
    /// <see cref="PermissionSyncGrpcService"/> konstruktori.
    /// </summary>
    public PermissionSyncGrpcService(
        IPermissionStorageHandler storageHandler,
        ILogger<PermissionSyncGrpcService> logger)
    {
        _storageHandler = storageHandler;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SyncPermissionsResponse> SyncPermissionsAsync(SyncPermissionsRequest request, CallContext context = default)
    {
        _logger.LogInformation(
            "gRPC orqali yangi ruxsatlar to'plami keldi. Servis: {ServiceName}, Soni: {Count}",
            request.ServiceName,
            request.Permissions.Count);

        try
        {
            var processed = await _storageHandler.HandlePermissionsReceivedAsync(
                request.ServiceName,
                request.ServiceCode,
                request.Permissions,
                context.CancellationToken);

            return new SyncPermissionsResponse
            {
                Success = true,
                ProcessedCount = processed,
                Message = $"{request.ServiceName} servisining {processed} ta ruxsati muvaffaqiyatli saqlandi."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ServiceName} servisining ruxsatlarini saqlashda xatolik yuz berdi", request.ServiceName);
            return new SyncPermissionsResponse
            {
                Success = false,
                ProcessedCount = 0,
                Message = $"Xatolik: {ex.Message}"
            };
        }
    }
}
