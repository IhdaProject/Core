using System.Diagnostics;
using Serilog;

namespace Core.Domain.Helpers;

public class CustomHttpClientHandler : DelegatingHandler
{
    public CustomHttpClientHandler() : this(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 100
    })
    {
    }

    public CustomHttpClientHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Log.Information("Request started: {Method} URL: {Url}",
                request.Method.Method,
                request.RequestUri?.ToString());

            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
            Log.Information("Request finished: {Method} URL: {Url} - {ElapsedSeconds:F3} s.",
                request.Method.Method,
                request.RequestUri?.ToString(),
                stopwatch.Elapsed.TotalSeconds
            );
        }
    }
}
