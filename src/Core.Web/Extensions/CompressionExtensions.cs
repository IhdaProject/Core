using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Web.Extensions;

/// <summary>
/// Yuqori unumdorlikka ega tarmoq trafigini siqish (Response Compression) kengaytmalari.
/// Brotli (Fastest) va Gzip orqali faqat matnli va JSON payloadlarni siqib, CPU resursini tejaydi.
/// </summary>
public static class CompressionExtensions
{
    private static readonly string[] CompressionMimeTypes =
    [
        "application/json",
        "application/problem+json",
        "application/xml",
        "text/plain",
        "text/json",
        "text/xml",
        "text/html",
        "application/javascript"
    ];

    /// <summary>
    /// Brotli va Gzip javob kompressiyasini sozlaydi.
    /// Rasmlar yoki PDF kabi oldindan siqilgan ikkilik fayllarni qayta siqib CPU sarflamaydi.
    /// </summary>
    /// <param name="services">DI servislar to'plami.</param>
    /// <returns>Servislar to'plami.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCoreCompression();
    /// // Pipeline da:
    /// app.UseResponseCompression();
    /// </code>
    /// </example>
    public static IServiceCollection AddCoreCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(CompressionMimeTypes).Distinct();
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        return services;
    }
}
