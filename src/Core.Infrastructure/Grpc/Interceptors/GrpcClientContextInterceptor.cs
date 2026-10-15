using Core.Domain.Common.Context;
using Core.Domain.Constants;
using Core.Domain.Enums;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;

namespace Core.Infrastructure.Grpc.Interceptors;

/// <summary>
/// Chiquvchi Code-First gRPC mijoz chaqiruvlari uchun interceptor.
/// Har bir chaqiruvga quyidagilarni avtomatik tarzda qo'shadi:
/// 1. X-Correlation-Id (so'rovlar zanjirini kuzatish uchun).
/// 2. X-Tenant-Id va X-Tenant-Scope (ko'p ijarachilik konteksti).
/// 3. X-User-Id va Authorization token (foydalanuvchi ma'lumotlari).
/// 4. W3C traceparent (OpenTelemetry telemetriyasi).
/// 5. 10 soniyalik majburiy Deadline va kiruvchi HTTP so'rovning RequestAborted tokeni
///    (agar brauzer/foydalanuvchi so'rovni bekor qilsa, ichki gRPC chaqiruvi ham zudlik bilan to'xtatiladi).
/// </summary>
public class GrpcClientContextInterceptor : Interceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// <see cref="GrpcClientContextInterceptor"/> konstruktori.
    /// </summary>
    public GrpcClientContextInterceptor(
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var enriched = EnrichContext(context);
        return continuation(request, enriched);
    }

    /// <inheritdoc />
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var enriched = EnrichContext(context);
        return continuation(request, enriched);
    }

    /// <inheritdoc />
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var enriched = EnrichContext(context);
        return continuation(request, enriched);
    }

    private ClientInterceptorContext<TRequest, TResponse> EnrichContext<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var headers = context.Options.Headers ?? new Metadata();
        var httpContext = _httpContextAccessor.HttpContext;

        // CorrelationId
        var correlationId = httpContext?.Request?.Headers[AppHeaderNames.CorrelationId].FirstOrDefault()
                            ?? httpContext?.Items[AppHeaderNames.CorrelationId]?.ToString()
                            ?? Guid.NewGuid().ToString("N");
        SafeAdd(headers, AppHeaderNames.CorrelationId.ToLowerInvariant(), correlationId);

        // OpenTelemetry TraceParent
        var traceparent = httpContext?.Request?.Headers[AppHeaderNames.TraceParent].FirstOrDefault();
        if (!string.IsNullOrEmpty(traceparent))
            SafeAdd(headers, AppHeaderNames.TraceParent, traceparent);

        // Authorization
        var authHeader = httpContext?.Request?.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader))
            SafeAdd(headers, "authorization", authHeader);

        // Tenant Context
        if (_tenantContext.TenantId.HasValue)
            SafeAdd(headers, AppHeaderNames.TenantId.ToLowerInvariant(), _tenantContext.TenantId.Value.ToString());

        SafeAdd(headers, AppHeaderNames.TenantScope.ToLowerInvariant(), _tenantContext.Scope.ToString().ToLowerInvariant());

        // User ID
        var userId = httpContext?.User?.FindFirst(CustomClaimNames.UserId)?.Value;
        if (!string.IsNullOrEmpty(userId))
            SafeAdd(headers, AppHeaderNames.UserId.ToLowerInvariant(), userId);

        // 10 soniyalik Deadline
        var deadline = context.Options.Deadline ?? DateTime.UtcNow.AddSeconds(10);
        var newOptions = context.Options.WithHeaders(headers).WithDeadline(deadline);

        // RequestAborted bilan bog'lash (Mijoz uzilsa, gRPC ham darhol bekor bo'ladi)
        if (httpContext is not null && httpContext.RequestAborted.CanBeCanceled)
        {
            var cancellationToken = context.Options.CancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(context.Options.CancellationToken, httpContext.RequestAborted).Token
                : httpContext.RequestAborted;

            newOptions = newOptions.WithCancellationToken(cancellationToken);
        }

        return new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, newOptions);
    }

    private static void SafeAdd(Metadata headers, string key, string value)
    {
        if (!headers.Any(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            headers.Add(key, value);
    }
}
