using Core.Domain.Common.Context;
using Core.Domain.Constants;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Core.Infrastructure.Grpc.Interceptors;

/// <summary>
/// Kiruvchi Code-First gRPC so'rovlari uchun server interceptori.
/// Boshqa mikroservislardan yuborilgan metadata sarlavhalarini (TenantId, CorrelationId)
/// o'qib, so'rov davomida <see cref="ITenantContext"/> ni to'g'ri tiklaydi.
/// </summary>
public class GrpcServerContextInterceptor : Interceptor
{
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// <see cref="GrpcServerContextInterceptor"/> konstruktori.
    /// </summary>
    /// <param name="tenantContext">Ijarachi konteksti.</param>
    public GrpcServerContextInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var tenantHeader = context.RequestHeaders.FirstOrDefault(h =>
            h.Key.Equals(AppHeaderNames.TenantId, StringComparison.OrdinalIgnoreCase));

        long? tenantId = null;
        if (tenantHeader != null && long.TryParse(tenantHeader.Value, out var parsedTenantId))
        {
            tenantId = parsedTenantId;
        }

        using (_tenantContext.Change(tenantId))
        {
            return await continuation(request, context);
        }
    }
}
