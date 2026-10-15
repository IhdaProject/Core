namespace Core.Domain.Common.Context;

/// <summary>
/// Ko'p ijarachili (Multi-Tenant) jadvallar uchun interfeys.
/// Ushbu interfeysni amalga oshirgan barcha entitiylar avtomatik ravishda
/// EF Core Global Query Filter orqali faqat tegishli ijarachiga ajratiladi
/// hamda (TenantId, IsDelete) kompozit B-Tree indeksi bilan ta'minlanadi.
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// Ushbu yozuv tegishli bo'lgan ijarachining unikal identifikatori.
    /// Agar yozuv butun tizim uchun umumiy bo'lsa null bo'lishi mumkin.
    /// </summary>
    long? TenantId { get; set; }
}
