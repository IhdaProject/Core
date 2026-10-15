using Core.Domain.Common.Context;
using MassTransit;

namespace Core.Infrastructure.Messaging.Filters;

/// <summary>
/// RabbitMQ ga xabar yuborilayotganda (Publish) joriy ijarachi (TenantId va TenantCode)
/// ma'lumotlarini xabar sarlavhalariga (Headers) avtomatik biriktiruvchi filtr.
/// </summary>
/// <typeparam name="T">Hodisa yoki xabar turi.</typeparam>
public class MassTransitTenantPublishFilter<T> : IFilter<PublishContext<T>>
    where T : class
{
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// <see cref="MassTransitTenantPublishFilter{T}"/> konstruktori.
    /// </summary>
    /// <param name="tenantContext">Ijarachi konteksti.</param>
    public MassTransitTenantPublishFilter(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        if (_tenantContext.TenantId.HasValue)
        {
            context.Headers.Set("TenantId", _tenantContext.TenantId.Value.ToString());
        }

        if (!string.IsNullOrEmpty(_tenantContext.TenantCode))
        {
            context.Headers.Set("TenantCode", _tenantContext.TenantCode);
        }

        context.Headers.Set("TenantScope", _tenantContext.Scope.ToString());

        await next.Send(context);
    }

    /// <inheritdoc />
    public void Probe(ProbeContext context) => context.CreateFilterScope("tenantPublishFilter");
}

/// <summary>
/// RabbitMQ ga to'g'ridan-to'g'ri nuqtama-nuqta (Send) xabar yuborilganda
/// ijarachi ma'lumotlarini sarlavhaga biriktiruvchi filtr.
/// </summary>
/// <typeparam name="T">Xabar turi.</typeparam>
public class MassTransitTenantSendFilter<T> : IFilter<SendContext<T>>
    where T : class
{
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// <see cref="MassTransitTenantSendFilter{T}"/> konstruktori.
    /// </summary>
    /// <param name="tenantContext">Ijarachi konteksti.</param>
    public MassTransitTenantSendFilter(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        if (_tenantContext.TenantId.HasValue)
        {
            context.Headers.Set("TenantId", _tenantContext.TenantId.Value.ToString());
        }

        if (!string.IsNullOrEmpty(_tenantContext.TenantCode))
        {
            context.Headers.Set("TenantCode", _tenantContext.TenantCode);
        }

        context.Headers.Set("TenantScope", _tenantContext.Scope.ToString());

        await next.Send(context);
    }

    /// <inheritdoc />
    public void Probe(ProbeContext context) => context.CreateFilterScope("tenantSendFilter");
}

/// <summary>
/// RabbitMQ navbatidan xabar iste'mol qilinganda (Consume), xabar sarlavhasidagi
/// TenantId va TenantCode ni o'qib, consumer doirasida <see cref="ITenantContext"/> ni
/// avtomatik tiklab beruvchi filtr.
/// </summary>
/// <typeparam name="T">Iste'mol qilinayotgan xabar turi.</typeparam>
public class MassTransitTenantConsumeFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// <see cref="MassTransitTenantConsumeFilter{T}"/> konstruktori.
    /// </summary>
    /// <param name="tenantContext">Ijarachi konteksti.</param>
    public MassTransitTenantConsumeFilter(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var tenantIdStr = context.Headers.Get<string>("TenantId");
        var tenantCode = context.Headers.Get<string>("TenantCode");

        long? tenantId = null;
        if (long.TryParse(tenantIdStr, out var tid))
        {
            tenantId = tid;
        }

        using (_tenantContext.Change(tenantId, tenantCode))
        {
            await next.Send(context);
        }
    }

    /// <inheritdoc />
    public void Probe(ProbeContext context) => context.CreateFilterScope("tenantConsumeFilter");
}
