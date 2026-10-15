using System.Text.Json;
using Core.Domain.Constants;
using Core.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Core.Web.Middlewares;

/// <summary>
/// Sessiya va tokenni bekor qilinganligini (revocation) tekshiruvchi middleware.
/// Foydalanuvchi tizimdan chiqqanida (Logout) yoki parolini o'zgartirganida,
/// JWT token muddati hali tugamagan bo'lsa ham, uning sessiyasi bekor qilinganini darhol aniqlaydi.
/// 
/// Unumdorlik:
/// Har bir so'rovda Redis'ga borib ortiqcha tarmoq yuklamasi qilmaslik uchun
/// tekshiruv natijasi L1 In-Memory keshda 20 soniya saqlanadi (0.001ms tekshiruv).
/// </summary>
public class SessionValidationMiddleware
{
    private static readonly TimeSpan L1Ttl = TimeSpan.FromSeconds(20);

    private readonly RequestDelegate _next;
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    /// <summary>
    /// <see cref="SessionValidationMiddleware"/> konstruktori.
    /// </summary>
    public SessionValidationMiddleware(
        RequestDelegate next,
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _logger = logger;
    }

    /// <summary>
    /// Middleware chaqiruvi.
    /// </summary>
    /// <param name="context">HTTP so'rov konteksti.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = user.FindFirst(CustomClaimNames.UserId)?.Value;
            var sessionId = user.FindFirst(CustomClaimNames.SessionId)?.Value;
            var securityStamp = user.FindFirst(CustomClaimNames.SecurityStamp)?.Value;

            if (long.TryParse(userIdClaim, out var userId) &&
                (!string.IsNullOrEmpty(sessionId) || !string.IsNullOrEmpty(securityStamp)))
            {
                var isRevoked = await CheckIsRevokedAsync(userId, sessionId, securityStamp);
                if (isRevoked)
                {
                    _logger.LogWarning("Sessiya yoki token bekor qilingan! UserId: {UserId}, SessionId: {SessionId}",
                        userId, sessionId);

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        success = false,
                        code = StatusCodes.Status401Unauthorized,
                        message = "Ushbu sessiya yoki token bekor qilingan. Iltimos, qayta tizimga kiring.",
                        error = "token_revoked"
                    }));
                    return;
                }
            }
        }

        await _next(context);
    }

    private async Task<bool> CheckIsRevokedAsync(long userId, string? sessionId, string? securityStamp)
    {
        var tokenIdentifier = sessionId ?? securityStamp ?? string.Empty;
        var l1Key = string.Format(AppCacheKeys.SessionRevokedL1Format, userId, tokenIdentifier);

        // 1. L1 In-Memory keshni tekshirish (0.001 ms)
        if (_memoryCache.TryGetValue(l1Key, out bool isRevokedInMemory))
        {
            return isRevokedInMemory;
        }

        // 2. L2 Redis keshini tekshirish
        var redisKey = string.Format(AppCacheKeys.SessionRevokedL2Format, userId, tokenIdentifier);
        var redisVal = await _distributedCache.GetStringAsync(redisKey);
        var isRevoked = !string.IsNullOrEmpty(redisVal);

        // Natijani L1 keshga 20 soniyaga saqlaymiz
        _memoryCache.Set(l1Key, isRevoked, L1Ttl);

        return isRevoked;
    }
}
