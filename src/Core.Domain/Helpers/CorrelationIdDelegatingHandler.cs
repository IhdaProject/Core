using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Core.Domain.Helpers;

public class CorrelationIdDelegatingHandler(IHttpContextAccessor? httpContextAccessor = null) : DelegatingHandler
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(CorrelationIdHeaderName))
        {
            var correlationId = httpContextAccessor?.HttpContext?.TraceIdentifier
                                ?? Activity.Current?.GetTagItem("correlation.id")?.ToString()
                                ?? Activity.Current?.Id
                                ?? Guid.NewGuid().ToString("N");

            request.Headers.Add(CorrelationIdHeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
