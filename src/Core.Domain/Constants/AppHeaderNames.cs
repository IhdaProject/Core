namespace Core.Domain.Constants;

/// <summary>
/// Mikroservislararo va tashqi HTTP/gRPC so'rovlarda ishlatiladigan standart sarlavha (header) nomlari.
/// </summary>
public static class AppHeaderNames
{
    /// <summary>
    /// Tarqatilgan tizimlarda so'rovlar zanjirini kuzatish uchun unikal identifikator.
    /// </summary>
    public const string CorrelationId = "X-Correlation-Id";

    /// <summary>
    /// Takroriy so'rovlarni (dublikatlarni) oldini olish uchun Fintech-standart idempotensiya kaliti.
    /// </summary>
    public const string IdempotencyKey = "X-Idempotency-Key";

    /// <summary>
    /// Joriy ijarachi identifikatori.
    /// </summary>
    public const string TenantId = "X-Tenant-Id";

    /// <summary>
    /// Joriy ijarachi qamrovi (masalan: "tenant", "global").
    /// </summary>
    public const string TenantScope = "X-Tenant-Scope";

    /// <summary>
    /// Joriy foydalanuvchi identifikatori.
    /// </summary>
    public const string UserId = "X-User-Id";

    /// <summary>
    /// W3C Distributed Tracing traceparent sarlavhasi.
    /// </summary>
    public const string TraceParent = "traceparent";

    /// <summary>
    /// Kesh holatini ko'rsatuvchi javob sarlavhasi (masalan: "HIT", "MISS", "HIT-IDEMPOTENT").
    /// </summary>
    public const string XCache = "X-Cache";

    /// <summary>
    /// Mijozning haqiqiy IP manzili (Reverse proxy / Ingress ortida).
    /// </summary>
    public const string XForwardedFor = "X-Forwarded-For";
}
