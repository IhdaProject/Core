using System.Text;
using System.Text.RegularExpressions;
using Core.Domain.Helpers;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Core.Web.Middlewares;

public class RequestResponseLoggingMiddleware : IMiddleware
{
    private const int MaxBodyLength = 32 * 1024; // 32 KB limit to prevent RAM exhaustion (OOM)

    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "ApiKey",
        "Token",
        "Proxy-Authorization"
    };

    private static readonly Regex SensitiveJsonFieldsRegex = new(
        @"""(?i)(password|pwd|secret|token|accesstoken|refreshtoken|apikey|creditcard|cardnumber|cvv|pin|salt|hash)""\s*:\s*""[^""]*""",
        RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var requestLog = await FormatRequestAsync(context);
        var originalBodyStream = context.Response.Body;

        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await next(context);
        }
        finally
        {
            var responseLog = await FormatResponseAsync(context);
            Log.Information("HTTP {Method} {Path} -> {StatusCode}\nRequest: {Request}\nResponse: {Response}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                requestLog,
                responseLog);

            if (responseBody.Length > 0)
            {
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }
    }

    private static async Task<string> FormatRequestAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        var body = string.Empty;
        var contentType = context.Request.ContentType ?? string.Empty;

        if (IsBinaryOrMultipart(contentType))
        {
            body = $"[Binary/Multipart content: {contentType}, Size: {context.Request.ContentLength ?? 0} bytes]";
        }
        else if (context.Request.ContentLength > 0 && context.Request.Body.CanRead)
        {
            context.Request.Body.Position = 0;
            var buffer = new char[Math.Min((int)context.Request.ContentLength.Value, MaxBodyLength)];
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var readCount = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
            body = new string(buffer, 0, readCount);
            if (context.Request.ContentLength.Value > MaxBodyLength)
            {
                body += "... [TRUNCATED due to size limit]";
            }
            context.Request.Body.Position = 0;

            body = MaskSensitiveData(body);
        }

        var requestInfo = new
        {
            Scheme = context.Request.Scheme,
            Host = context.Request.Host.ToString(),
            Path = context.Request.Path.Value,
            QueryString = context.Request.QueryString.ToString(),
            Method = context.Request.Method,
            Headers = MaskHeaders(context.Request.Headers),
            Cookies = context.Request.Cookies.Keys.ToDictionary(k => k, _ => "[REDACTED]"),
            Body = body
        };

        return SerializerHelper.ToJsonString(requestInfo);
    }

    private static async Task<string> FormatResponseAsync(HttpContext context)
    {
        var contentType = context.Response.ContentType ?? string.Empty;
        var bodyText = string.Empty;

        if (IsBinaryOrMultipart(contentType))
        {
            bodyText = $"[Binary content: {contentType}, Size: {context.Response.Body.Length} bytes]";
        }
        else if (context.Response.Body.CanRead && context.Response.Body.Length > 0)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var lengthToRead = (int)Math.Min(context.Response.Body.Length, MaxBodyLength);
            var buffer = new char[lengthToRead];
            using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
            var readCount = await reader.ReadBlockAsync(buffer, 0, lengthToRead);
            bodyText = new string(buffer, 0, readCount);
            if (context.Response.Body.Length > MaxBodyLength)
            {
                bodyText += "... [TRUNCATED due to size limit]";
            }
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            bodyText = MaskSensitiveData(bodyText);
        }

        var responseInfo = new
        {
            StatusCode = context.Response.StatusCode,
            Headers = MaskHeaders(context.Response.Headers),
            Body = bodyText
        };

        return SerializerHelper.ToJsonString(responseInfo);
    }

    private static bool IsBinaryOrMultipart(string contentType)
    {
        if (string.IsNullOrEmpty(contentType)) return false;
        return contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("image/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("audio/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("video/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("application/pdf", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("application/zip", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> MaskHeaders(IHeaderDictionary headers)
    {
        var result = new Dictionary<string, string>();
        foreach (var header in headers)
        {
            if (SensitiveHeaders.Contains(header.Key))
            {
                result[header.Key] = "[REDACTED]";
            }
            else
            {
                result[header.Key] = header.Value.ToString();
            }
        }
        return result;
    }

    private static string MaskSensitiveData(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;
        return SensitiveJsonFieldsRegex.Replace(content, match =>
        {
            var colonIndex = match.Value.IndexOf(':');
            if (colonIndex >= 0)
            {
                return match.Value[..(colonIndex + 1)] + " \"***\"";
            }
            return match.Value;
        });
    }
}
