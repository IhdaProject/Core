using System.Threading.RateLimiting;
using Core.Domain.Constants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Web.Extensions;

/// <summary>
/// Taqsimlangan Rate Limiting (so'rovlar chastotasini cheklash) kengaytmalari.
/// DoS, bruteforce va bitta mijozning butun servisni band qilib qo'yishi (Noisy Neighbor) dan himoya qiladi.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Har bir mijoz IP manzili bo'yicha mustaqil darchali (Sliding Window) Rate Limiter ni sozlaydi.
    /// Sukut bo'yicha har bir IP ga soniyasiga 100 ta so'rov ruxsat etiladi.
    /// </summary>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="permitLimit">Har bir darcha uchun ruxsat etilgan so'rovlar soni (sukut bo'yicha 100).</param>
    /// <param name="windowSeconds">Darcha davomiyligi soniyalarda (sukut bo'yicha 1s).</param>
    /// <param name="queueLimit">Navbatda kutishi mumkin bo'lgan so'rovlar soni (sukut bo'yicha 20).</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCoreRateLimiting(permitLimit: 100, windowSeconds: 1);
    /// // Pipeline da:
    /// app.UseRateLimiter();
    /// </code>
    /// </example>
    public static IServiceCollection AddCoreRateLimiting(
        this IServiceCollection services,
        int permitLimit = 100,
        int windowSeconds = 1,
        int queueLimit = 20)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? httpContext.Request.Headers[AppHeaderNames.XForwardedFor].FirstOrDefault()?.Split(',')[0].Trim()
                    ?? "unknown-client";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    clientIp,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        SegmentsPerWindow = 2,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = queueLimit
                    });
            });

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync("{\"success\":false,\"code\":429,\"message\":\"Juda ko'p so'rov yuborildi. Iltimos, birozdan so'ng qayta urining.\",\"error\":\"too_many_requests\"}", token);
            };
        });

        return services;
    }
}
