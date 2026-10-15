using Core.Domain.Common.Context;
using Core.Domain.Common.Lock;
using Core.Infrastructure.Caching.Hybrid;
using Core.Infrastructure.Lock;
using Core.Web.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using StackExchange.Redis;

namespace Core.Web.Extensions;

/// <summary>
/// Core kutubxonasining barcha yangi korporativ modullarini DI ga oson ro'yxatdan o'tkazish
/// va middleware pipeline ga ulash uchun yagona kengaytma to'plami.
/// </summary>
public static class WebCoreExtensions
{
    /// <summary>
    /// Tuned Singleton NpgsqlDataSource ni ro'yxatdan o'tkazadi.
    /// Statement Auto-Preparation (MaxAutoPrepare=50, AutoPrepareMinUsages=2),
    /// MinPoolSize=10, KeepAlive=30s va DynamicJson ni faollashtiradi.
    /// Server darajasida SQL execution planlarni keshlab, bazaga tushadigan parsing yukini yo'qotadi.
    /// </summary>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="connectionString">PostgreSQL ulanish satri.</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCoreNpgsqlDataSource(connectionString);
    /// </code>
    /// </example>
    public static IServiceCollection AddCoreNpgsqlDataSource(
        this IServiceCollection services,
        string connectionString)
    {
        services.TryAddSingleton(sp =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

            if (dataSourceBuilder.ConnectionStringBuilder.MinPoolSize == 0)
                dataSourceBuilder.ConnectionStringBuilder.MinPoolSize = 10;

            if (dataSourceBuilder.ConnectionStringBuilder.KeepAlive == 0)
                dataSourceBuilder.ConnectionStringBuilder.KeepAlive = 30;

            if (dataSourceBuilder.ConnectionStringBuilder.MaxAutoPrepare == 0)
            {
                dataSourceBuilder.ConnectionStringBuilder.MaxAutoPrepare = 50;
                dataSourceBuilder.ConnectionStringBuilder.AutoPrepareMinUsages = 2;
            }

            dataSourceBuilder.EnableDynamicJson();
            return dataSourceBuilder.Build();
        });

        return services;
    }

    /// <summary>
    /// Redis taqsimlangan qulflash (Distributed Lock) mexanizmini ro'yxatdan o'tkazadi.
    /// Auto-renewal watchdog va random jitter bilan ta'minlangan.
    /// </summary>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="redisConnectionString">Redis ulanish satri.</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCoreDistributedLock(builder.Configuration.GetConnectionString("Redis"));
    /// </code>
    /// </example>
    public static IServiceCollection AddCoreDistributedLock(
        this IServiceCollection services,
        string? redisConnectionString)
    {
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.TryAddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConnectionString));
            services.TryAddSingleton<IDistributedLock, RedisDistributedLock>();
        }

        return services;
    }

    /// <summary>
    /// .NET 10 rasmiy HybridCache (L1 Memory + L2 Redis) xizmatini ro'yxatdan o'tkazadi.
    /// Lock-on-Read orqali Cache Stampede dan himoyalaydi.
    /// </summary>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="redisConnectionString">Redis ulanish satri (agar bo'lsa L2 kesh faollashadi).</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCoreHybridCache(builder.Configuration.GetConnectionString("Redis"));
    /// </code>
    /// </example>
    public static IServiceCollection AddCoreHybridCache(
        this IServiceCollection services,
        string? redisConnectionString = null)
    {
        services.AddMemoryCache();

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
            });
        }

#pragma warning disable EXTEXP0018 // HybridCache is currently experimental/preview in some frameworks
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromSeconds(30),
                Expiration = TimeSpan.FromMinutes(5)
            };
        });
#pragma warning restore EXTEXP0018

        services.TryAddScoped<IHybridCacheService, HybridCacheService>();

        return services;
    }

    /// <summary>
    /// Fintech-darajasidagi takroriy chaqiruvlarni bloklovchi Idempotency middleware'ini ro'yxatdan o'tkazadi.
    /// </summary>
    public static IServiceCollection AddCoreIdempotency(this IServiceCollection services)
    {
        services.AddTransient<IdempotencyMiddleware>();
        return services;
    }

    /// <summary>
    /// Token va sessiyalarning bekor qilinganligini (Revocation) tekshiruvchi middleware'ni ro'yxatdan o'tkazadi.
    /// </summary>
    public static IServiceCollection AddCoreSessionValidation(this IServiceCollection services)
    {
        services.AddTransient<SessionValidationMiddleware>();
        return services;
    }

    /// <summary>
    /// Xavfsizlik sarlavhalari (Security Headers) middleware'ini ro'yxatdan o'tkazadi.
    /// </summary>
    public static IServiceCollection AddCoreSecurityHeaders(this IServiceCollection services)
    {
        services.AddTransient<SecurityHeadersMiddleware>();
        return services;
    }

    /// <summary>
    /// Korporativ xavfsizlik sarlavhalarini HTTP pipeline ga ulaydi.
    /// </summary>
    public static IApplicationBuilder UseCoreSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }

    /// <summary>
    /// Idempotency middleware'ini HTTP pipeline ga ulaydi.
    /// </summary>
    public static IApplicationBuilder UseCoreIdempotency(this IApplicationBuilder app)
    {
        return app.UseMiddleware<IdempotencyMiddleware>();
    }

    /// <summary>
    /// Sessiyani bekor qilish (Revocation) middleware'ini HTTP pipeline ga ulaydi.
    /// </summary>
    public static IApplicationBuilder UseCoreSessionValidation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionValidationMiddleware>();
    }
}
