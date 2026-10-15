using Core.Infrastructure.Grpc.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc.ClientFactory;
using ProtoBuf.Grpc.Server;

namespace Core.Infrastructure.Grpc.Extensions;

/// <summary>
/// Code-First gRPC server va mijozlarini (protobuf-net.Grpc) oson ro'yxatga olish kengaytmalari.
/// Qo'lda .proto fayl yozish shart emas, toza C# interfeys va modellar orqali ishlaydi.
/// </summary>
public static class GrpcServiceExtensions
{
    /// <summary>
    /// Code-First gRPC serverini ro'yxatdan o'tkazadi.
    /// Kontekst va istisnolar interceptorlari, 16MB maksimal xabar hajmi hamda Fastest javob kompressiyasini o'rnatadi.
    /// </summary>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="maxMessageSizeInBytes">Maksimal qabul qilinadigan va yuboriladigan xabar hajmi baytlarda (sukut bo'yicha 16MB).</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCoreGrpcServer();
    /// // MapGrpcService orqali kontrollerga o'xshash xizmatni ochish:
    /// app.MapGrpcService&lt;OrderGrpcService&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddCoreGrpcServer(
        this IServiceCollection services,
        int maxMessageSizeInBytes = 16 * 1024 * 1024)
    {
        services.AddTransient<GrpcServerContextInterceptor>();
        services.AddTransient<GrpcExceptionInterceptor>();

        services.AddCodeFirstGrpc(options =>
        {
            options.MaxReceiveMessageSize = maxMessageSizeInBytes;
            options.MaxSendMessageSize = maxMessageSizeInBytes;
            options.ResponseCompressionLevel = System.IO.Compression.CompressionLevel.Fastest;
            options.Interceptors.Add<GrpcServerContextInterceptor>();
            options.Interceptors.Add<GrpcExceptionInterceptor>();
        });

        return services;
    }

    /// <summary>
    /// Boshqa mikroservis bilan bog'lanish uchun Code-First gRPC mijozini ro'yxatdan o'tkazadi.
    /// HTTP/2 ko'p oqimli aloqa (EnableMultipleHttp2Connections), KeepAlive Ping
    /// hamda avtomatik TenantId, CorrelationId va Deadline uzatuvchi interceptor bilan jihozlangan.
    /// </summary>
    /// <typeparam name="TClient">gRPC xizmat interfeysi (masalan: IBillingGrpcService).</typeparam>
    /// <param name="services">DI servislar to'plami.</param>
    /// <param name="address">Maqsad mikroservis manzili (masalan: "http://billing-service:5001").</param>
    /// <param name="maxMessageSizeInBytes">Maksimal xabar hajmi baytlarda (sukut bo'yicha 16MB).</param>
    /// <returns>HttpClientBuilder.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCoreGrpcClient&lt;IBillingGrpcService&gt;("http://billing-service:5001");
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddCoreGrpcClient<TClient>(
        this IServiceCollection services,
        string address,
        int maxMessageSizeInBytes = 16 * 1024 * 1024)
        where TClient : class
    {
        services.AddTransient<GrpcClientContextInterceptor>();

        return services
            .AddCodeFirstGrpcClient<TClient>(o =>
            {
                o.Address = new Uri(address);
                o.ChannelOptionsActions.Add(opt =>
                {
                    opt.MaxReceiveMessageSize = maxMessageSizeInBytes;
                    opt.MaxSendMessageSize = maxMessageSizeInBytes;
                });
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
            })
            .AddInterceptor<GrpcClientContextInterceptor>();
    }
}
