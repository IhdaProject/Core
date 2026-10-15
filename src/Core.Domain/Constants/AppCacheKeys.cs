namespace Core.Domain.Constants;

/// <summary>
/// Kesh kalitlari formatlari va andozalari.
/// </summary>
public static class AppCacheKeys
{
    /// <summary>
    /// Idempotency kesh kaliti formati: idempotency:{tenant_id}:{path}:{key}
    /// </summary>
    public const string IdempotencyKeyFormat = "idempotency:{0}:{1}:{2}";

    /// <summary>
    /// L1 In-Memory sessiya bekor qilinganligini tekshirish kaliti formati: session_revoked:l1:{user_id}:{token_id}
    /// </summary>
    public const string SessionRevokedL1Format = "session_revoked:l1:{0}:{1}";

    /// <summary>
    /// L2 Redis sessiya bekor qilinganligini tekshirish kaliti formati: session_revoked:l2:{user_id}:{token_id}
    /// </summary>
    public const string SessionRevokedL2Format = "session_revoked:l2:{0}:{1}";
}
