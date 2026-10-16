using System.ServiceModel;
using Core.Domain.Permissions;
using ProtoBuf.Grpc;

namespace Core.Infrastructure.Permissions.Grpc;

/// <summary>
/// Ruxsatlarni AuthApi ga sinxron ravishda ro'yxatdan o'tkazish uchun Code-First gRPC xizmati interfeysi.
/// </summary>
[ServiceContract]
public interface IPermissionSyncGrpcService
{
    /// <summary>
    /// Mikroservis ruxsatlarini AuthApi ga yuborish va bazaga saqlash/yangilash metodi.
    /// </summary>
    /// <param name="request">Ruxsatlar ro'yxatini o'z ichiga olgan so'rov.</param>
    /// <param name="context">gRPC chaqiruv konteksti.</param>
    /// <returns>Ro'yxatdan o'tganlik holati haqida javob.</returns>
    [OperationContract]
    Task<SyncPermissionsResponse> SyncPermissionsAsync(SyncPermissionsRequest request, CallContext context = default);
}
