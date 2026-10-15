using Core.Infrastructure.Messaging.Filters;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Infrastructure.Messaging.Extensions;

/// <summary>
/// MassTransit va PostgreSQL Transactional Outbox/Inbox ni oson ro'yxatdan o'tkazish uchun kengaytmalar.
/// </summary>
public static class MassTransitServiceExtensions
{
    /// <summary>
    /// Servis uchun MassTransit + RabbitMQ va PostgreSQL EF Core Transactional Outbox ni sozlaydi.
    /// Baza tranzaksiyasi commit bo'lgandagina xabar RabbitMQ ga yuboriladi (Dual-Write muammosini yo'qotadi).
    /// </summary>
    /// <typeparam name="TDbContext">Outbox jadvallarini o'z ichiga olgan DbContext turi.</typeparam>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="configuration">Konfiguratsiya obyekti (RabbitMQ parametrlarini o'qish uchun).</param>
    /// <param name="configureBus">Qo'shimcha consumerlar yoki handlerlarni ro'yxatga olish uchun aksiya.</param>
    /// <param name="configureRabbitMq">RabbitMQ xost va navbat parametrlarini qo'shimcha sozlash uchun aksiya.</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCoreMassTransit&lt;AppDbContext&gt;(builder.Configuration, bus =>
    /// {
    ///     bus.AddConsumer&lt;OrderCreatedConsumer&gt;();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddCoreMassTransit<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureRabbitMq = null)
        where TDbContext : DbContext
    {
        var rabbitSection = configuration.GetSection("RabbitMQ");
        var host = rabbitSection["Host"] ?? "localhost";
        var portStr = rabbitSection["Port"] ?? "5672";
        ushort.TryParse(portStr, out var port);
        if (port == 0) port = 5672;

        var username = rabbitSection["Username"] ?? "guest";
        var password = rabbitSection["Password"] ?? "guest";

        int? prefetchCount = int.TryParse(rabbitSection["PrefetchCount"], out var pfc) ? pfc : 32;
        int? concurrentLimit = int.TryParse(rabbitSection["ConcurrentMessageLimit"], out var cml) ? cml : 16;

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            configureBus?.Invoke(x);

            // PostgreSQL Transactional Outbox / Inbox konfiguratsiyasi
            x.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(host, port, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                if (prefetchCount.HasValue)
                {
                    cfg.PrefetchCount = prefetchCount.Value;
                }

                if (concurrentLimit.HasValue)
                {
                    cfg.ConcurrentMessageLimit = concurrentLimit.Value;
                }

                // Multi-Tenancy ma'lumotlarini RabbitMQ sarlavhalari orqali avtomatik uzatish va tiklash
                cfg.UsePublishFilter(typeof(MassTransitTenantPublishFilter<>), context);
                cfg.UseSendFilter(typeof(MassTransitTenantSendFilter<>), context);
                cfg.UseConsumeFilter(typeof(MassTransitTenantConsumeFilter<>), context);

                // Tarmoq uzilishlarida eksponentsial qayta urinish (1s, 2s, 4s...)
                cfg.UseMessageRetry(r => r.Exponential(
                    3,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromSeconds(2)));

                configureRabbitMq?.Invoke(context, cfg);

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
