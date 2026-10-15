namespace Core.Infrastructure.Caching.Hybrid;

/// <summary>
/// .NET 10 rasmiy HybridCache (L1 Memory + L2 Redis) xizmati interfeysi.
/// Ushbu xizmat bir vaqtning o'zida tezkor mahalliy xotira (In-Memory) va taqsimlangan (Redis)
/// keshni uyg'unlashtiradi, shuningdek, Cache Stampede (Dog-Piling) muammosini
/// Lock-on-Read (Single-Flight) orqali to'liq bartaraf etadi.
/// </summary>
public interface IHybridCacheService
{
    /// <summary>
    /// Keshdan kalit bo'yicha qiymatni oladi. Agar kalit keshda mavjud bo'lmasa,
    /// berilgan zavod (factory) funksiyasini chaqirib yangi qiymatni hisoblaydi va keshga saqlaydi.
    /// Bir vaqtda kelgan yuzlab parallel so'rovlarda factory faqat bir marta chaqiriladi (Single-Flight).
    /// </summary>
    /// <typeparam name="T">Keshlanayotgan ma'lumot turi.</typeparam>
    /// <param name="key">Kesh kaliti.</param>
    /// <param name="factory">Ma'lumot keshda topilmaganda uni yaratuvchi / bazadan oluvchi asinxron funksiya.</param>
    /// <param name="localTtl">L1 (In-Memory) xotirada saqlanish muddati (sukut bo'yicha 30 soniya).</param>
    /// <param name="remoteTtl">L2 (Redis) taqsimlangan keshda saqlanish muddati (sukut bo'yicha 5 daqiqa).</param>
    /// <param name="tags">Kesh yozuviga biriktiriladigan teglar (teglar bo'yicha o'chirish uchun).</param>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    /// <returns>Keshdagi yoki yangi hisoblangan qiymat.</returns>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan? localTtl = null,
        TimeSpan? remoteTtl = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Keshga to'g'ridan-to'g'ri yangi qiymat yozadi (yoki mavjudini yangilaydi).
    /// </summary>
    /// <typeparam name="T">Saqlanadigan qiymat turi.</typeparam>
    /// <param name="key">Kesh kaliti.</param>
    /// <param name="value">Saqlanadigan ma'lumot.</param>
    /// <param name="localTtl">L1 (In-Memory) xotirada saqlanish muddati.</param>
    /// <param name="remoteTtl">L2 (Redis) xotirada saqlanish muddati.</param>
    /// <param name="tags">Teglar ro'yxati.</param>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? localTtl = null,
        TimeSpan? remoteTtl = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ko'rsatilgan kalit bo'yicha kesh yozuvini (L1 va L2 dan) o'chiradi.
    /// </summary>
    /// <param name="key">O'chiriladigan kesh kaliti.</param>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ko'rsatilgan teg bilan belgilangan barcha bog'liq kesh yozuvlarini birdaniga bekor qiladi.
    /// Masalan: bitta mahsulot yangilanganda, unga tegishli barcha sahifalar yoki toifalar keshini tozalash.
    /// </summary>
    /// <param name="tag">Bekor qilinadigan teg nomi.</param>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
