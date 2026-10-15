using MassTransit;
using Microsoft.Extensions.Logging;

namespace Core.Infrastructure.Messaging.Consumers;

/// <summary>
/// Qayta ishlashda xatolik yuz bergan (Fault/Poison) xabarlarni tutib oluvchi bazaviy consumer.
/// Dead-letter va audit maqsadlarida xabar ma'lumotlari, istisno (exception) sabablari
/// va xatolik steki bo'yicha batafsil log yozadi.
/// </summary>
/// <typeparam name="TMessage">Xatolikka uchragan asl xabar turi.</typeparam>
public abstract class BaseFaultConsumer<TMessage> : IConsumer<Fault<TMessage>>
    where TMessage : class
{
    private readonly ILogger _logger;

    /// <summary>
    /// <see cref="BaseFaultConsumer{TMessage}"/> konstruktori.
    /// </summary>
    /// <param name="logger">Tizim logeri.</param>
    protected BaseFaultConsumer(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Xatolikka uchragan xabarni qayta ishlash metodi.
    /// </summary>
    /// <param name="context">Xatolik konteksti va asl xabar ma'lumotlari.</param>
    public virtual Task Consume(ConsumeContext<Fault<TMessage>> context)
    {
        var message = context.Message;
        var exceptions = message.Exceptions;

        _logger.LogError(
            "Xabarni iste'mol qilishda xatolik (Fault) yuz berdi. Xabar turi: {MessageType}, FaultId: {FaultId}, Xabarlar soni: {Count}",
            typeof(TMessage).Name,
            message.FaultId,
            exceptions.Length);

        foreach (var ex in exceptions)
        {
            _logger.LogError(
                "Xatolik tafsiloti: {ExceptionType} - {ErrorMessage}. Source: {Source}",
                ex.ExceptionType,
                ex.Message,
                ex.Source);
        }

        return OnFaultAsync(context);
    }

    /// <summary>
    /// Xatolik yuz berganda qo'shimcha maxsus biznes mantiqni (masalan: DB ga yozish, Telegram alert)
    /// bajarish uchun voris sinfda qayta aniqlanishi (override) mumkin bo'lgan metod.
    /// </summary>
    /// <param name="context">Xatolik konteksti.</param>
    protected virtual Task OnFaultAsync(ConsumeContext<Fault<TMessage>> context)
    {
        return Task.CompletedTask;
    }
}
