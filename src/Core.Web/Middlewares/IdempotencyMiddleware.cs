using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Domain.Common.Context;
using Core.Domain.Common.Lock;
using Core.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

namespace Core.Web.Middlewares;

/// <summary>
/// Fintech va to'lov standartlariga mos HTTP Idempotency middleware'i.
/// Foydalanuvchi tugmani ikki marta bosib yuborganda yoki tarmoq xatosi tufayli
/// bir xil POST/PUT so'rovi qayta yuborilganda tizimda takroriy (dublikat)
/// yozuvlar yaratilishining oldini oladi.
/// 
/// Asosiy imkoniyatlari:
/// 1. Taqsimlangan qulf (RedisDistributedLock) orqali ayni bir vaqtda parallel
///    kelgan dublikat so'rovlarni bloklaydi (409 Conflict).
/// 2. So'rov tanasi (body) ning SHA-256 xeshini tekshirib, bitta kalitdan
///    boshqa parametrlar bilan foydalanishga yo'l qo'ymaydi (idempotency_payload_mismatch).
/// 3. RecyclableMemoryStreamManager yordamida javobni LOH (Large Object Heap) ga
///    bosim bermasdan xotirada buferlaydi.
/// 4. Muvaffaqiyatli 2xx JSON javoblarini Redis'da 2 daqiqa keshlaydi va takroriy
///    so'rovga keshdan javob qaytaradi (X-Cache: HIT-IDEMPOTENT).
/// </summary>
public class IdempotencyMiddleware
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(2);
    private static readonly RecyclableMemoryStreamManager MemoryStreamManager = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    private readonly IDistributedLock _distributedLock;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    /// <summary>
    /// <see cref="IdempotencyMiddleware"/> konstruktori.
    /// </summary>
    public IdempotencyMiddleware(
        RequestDelegate next,
        IDistributedCache cache,
        IDistributedLock distributedLock,
        ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _distributedLock = distributedLock;
        _logger = logger;
    }

    /// <summary>
    /// Middleware chaqiruvi.
    /// </summary>
    /// <param name="context">HTTP so'rov konteksti.</param>
    /// <param name="tenantContext">Ijarachi konteksti.</param>
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Faqat POST va PUT so'rovlarida idempotentlik tekshiriladi
        if (!HttpMethods.IsPost(context.Request.Method) && !HttpMethods.IsPut(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Agar so'rov sarlavhasida X-Idempotency-Key bo'lmasa, oddiy oqim davom etadi
        if (!context.Request.Headers.TryGetValue(AppHeaderNames.IdempotencyKey, out var keyVal) ||
            string.IsNullOrWhiteSpace(keyVal))
        {
            await _next(context);
            return;
        }

        var key = keyVal.ToString().Trim();
        var tenantId = tenantContext.TenantId ?? 0;
        var cacheKey = string.Format(AppCacheKeys.IdempotencyKeyFormat, tenantId, context.Request.Path, key);
        var currentPayloadHash = await ComputeRequestPayloadHashAsync(context.Request, context.RequestAborted);

        // 1. Keshda oldingi javob mavjudligini tekshiramiz
        var cachedData = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var cached = JsonSerializer.Deserialize<CachedHttpResponse>(cachedData, JsonOpts);
            if (cached != null)
            {
                // Agar kalit bir xil bo'lib, so'rov tanasi boshqacha bo'lsa - xavfsizlik xatosi
                if (!string.IsNullOrEmpty(cached.RequestPayloadHash) && cached.RequestPayloadHash != currentPayloadHash)
                {
                    _logger.LogWarning("Idempotency-Key {Key} boshqa so'rov tanasi bilan qayta ishlatildi!", key);
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        success = false,
                        code = StatusCodes.Status409Conflict,
                        message = "Ushbu Idempotency-Key boshqa ma'lumotlar bilan ishlatilgan.",
                        error = "idempotency_payload_mismatch"
                    }));
                    return;
                }

                _logger.LogInformation("Idempotent so'rov uchun keshdan javob qaytarilmoqda: {Key}", key);
                context.Response.StatusCode = cached.StatusCode;
                context.Response.ContentType = cached.ContentType;
                context.Response.Headers[AppHeaderNames.IdempotencyKey] = key;
                context.Response.Headers[AppHeaderNames.XCache] = "HIT-IDEMPOTENT";
                await context.Response.WriteAsync(cached.Body);
                return;
            }
        }

        // 2. Parallel kelgan dublikat so'rovlarni to'xtatish uchun taqsimlangan qulf olamiz
        var lockKey = $"idempotency:{tenantId}:{key}";
        await using var lockHandle = await _distributedLock.TryAcquireLockAsync(
            lockKey,
            timeout: TimeSpan.FromMilliseconds(500),
            expiry: TimeSpan.FromSeconds(60),
            context.RequestAborted);

        if (lockHandle is null)
        {
            _logger.LogWarning("Ayni bir vaqtda parallel dublikat so'rov aniqlandi: {Key}", key);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                success = false,
                code = StatusCodes.Status409Conflict,
                message = "Ushbu so'rov ayni paytda boshqa oqimda qayta ishlanmoqda.",
                error = "request_in_progress"
            }));
            return;
        }

        // 3. Qulfni kutish paytida oldingi so'rov yakunlanib keshga yozilgan bo'lishi mumkinligini qayta tekshiramiz
        cachedData = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedData))
        {
            var cached = JsonSerializer.Deserialize<CachedHttpResponse>(cachedData, JsonOpts);
            if (cached != null)
            {
                context.Response.StatusCode = cached.StatusCode;
                context.Response.ContentType = cached.ContentType;
                context.Response.Headers[AppHeaderNames.IdempotencyKey] = key;
                context.Response.Headers[AppHeaderNames.XCache] = "HIT-IDEMPOTENT";
                await context.Response.WriteAsync(cached.Body);
                return;
            }
        }

        // 4. So'rovni bajaramiz va natijani RecyclableMemoryStream buferiga yozamiz
        var originalBodyStream = context.Response.Body;
        using var memoryStream = MemoryStreamManager.GetStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            var isJson = context.Response.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? true;
            var isCacheableSize = memoryStream.Length <= 1024 * 1024; // Faqat 1MB gacha bo'lgan javoblar keshlanadi

            if (context.Response.StatusCode is >= 200 and < 300 && isJson && isCacheableSize)
            {
                memoryStream.Position = 0;
                var responseBody = await new StreamReader(memoryStream, Encoding.UTF8).ReadToEndAsync();

                var cachedObj = new CachedHttpResponse
                {
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType ?? "application/json",
                    Body = responseBody,
                    RequestPayloadHash = currentPayloadHash
                };

                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = DefaultTtl
                };

                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(cachedObj, JsonOpts), cacheOptions);
            }

            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static async Task<string> ComputeRequestPayloadHashAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ContentLength == 0)
        {
            return string.Empty;
        }

        request.EnableBuffering();
        request.Body.Position = 0;

        var hashBytes = await SHA256.HashDataAsync(request.Body, cancellationToken);
        request.Body.Position = 0;

        return Convert.ToHexString(hashBytes);
    }

    private sealed class CachedHttpResponse
    {
        public int StatusCode { get; set; }
        public string ContentType { get; set; } = "application/json";
        public string Body { get; set; } = string.Empty;
        public string? RequestPayloadHash { get; set; }
    }
}
