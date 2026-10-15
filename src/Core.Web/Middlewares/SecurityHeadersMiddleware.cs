using Microsoft.AspNetCore.Http;

namespace Core.Web.Middlewares;

/// <summary>
/// Korporativ xavfsizlik sarlavhalari (Security Headers) middleware'i.
/// Clickjacking, MIME-sniffing, XSS va boshqa tarmoq hujumlaridan himoya qilish uchun
/// har bir chiquvchi javobga zarur xavfsizlik sarlavhalarini biriktiradi.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// <see cref="SecurityHeadersMiddleware"/> konstruktori.
    /// </summary>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Middleware chaqiruvi.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Frame-Options"] = "DENY";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-XSS-Protection"] = "1; mode=block";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        await _next(context);
    }
}
