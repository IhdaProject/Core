using Polly;

namespace Core.Infrastructure.Resilience;

public class ResilienceDelegatingHandler(ResiliencePipeline pipeline) : DelegatingHandler
{
    public ResilienceDelegatingHandler() : this(ResiliencePipelineExtensions.CreateDefaultHttpResiliencePipeline())
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await pipeline.ExecuteAsync(
            async ct => await base.SendAsync(request, ct),
            cancellationToken);
    }
}
