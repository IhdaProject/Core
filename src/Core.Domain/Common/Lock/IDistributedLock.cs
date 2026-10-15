namespace Core.Domain.Common.Lock;

/// <summary>
/// Taqsimlangan qulflash (Distributed Lock) abstraksiyasi.
/// Bir nechta mikroservis nusxalari (instancelari) yoki fon vazifalari bir vaqtning o'zida
/// bitta umumiy resursni o'zgartirishi (race condition / lock stampede) oldini oladi.
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Belgilangan resurs kaliti bo'yicha qulf olishga urinadi.
    /// Agar qulf muvaffaqiyatli olinsa, uni avtomatik bo'shatuvchi <see cref="IAsyncDisposable"/> qaytaradi.
    /// Qulfni uzoq operatsiyalar paytida muddati tugab qolmasligi uchun fon mexanizmi (watchdog) avtomatik uzaytiradi.
    /// </summary>
    /// <param name="resourceKey">Qulflanadigan yagona resurs nomi (masalan: "order:12345", "user_sync").</param>
    /// <param name="timeout">Qulfni olish uchun maksimal kutish vaqti.</param>
    /// <param name="expiry">Qulfning amal qilish muddati (agar jarayon halokatga uchrasa, avtomatik ochilish vaqti).</param>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    /// <returns>Qulf olingan bo'lsa <see cref="IAsyncDisposable"/> obyekti, aks holda null.</returns>
    /// <example>
    /// <code>
    /// await using var lockHandle = await distributedLock.TryAcquireLockAsync("payment:1001", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
    /// if (lockHandle == null)
    /// {
    ///     throw new ConflictException("Ushbu to'lov hozirda qayta ishlanmoqda!");
    /// }
    /// // Tranzaksiyani bajarish...
    /// </code>
    /// </example>
    Task<IAsyncDisposable?> TryAcquireLockAsync(
        string resourceKey,
        TimeSpan timeout,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belgilangan resurs kaliti ayni vaqtda boshqa jarayon tomonidan qulflanganligini tekshiradi.
    /// </summary>
    /// <param name="resourceKey">Tekshirilayotgan resurs kaliti.</param>
    /// <param name="cancellationToken">Bekor qilish tokeni.</param>
    /// <returns>Agar resurs qulflangan bo'lsa true, aks holda false.</returns>
    Task<bool> IsLockedAsync(string resourceKey, CancellationToken cancellationToken = default);
}
