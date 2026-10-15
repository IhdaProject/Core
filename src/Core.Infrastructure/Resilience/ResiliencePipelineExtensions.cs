using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Core.Infrastructure.Resilience;

public static class ResiliencePipelineExtensions
{
    public static ResiliencePipeline CreateDefaultHttpResiliencePipeline(
        int retryCount = 3,
        TimeSpan? retryDelay = null,
        double failureRatio = 0.5,
        TimeSpan? breakDuration = null,
        TimeSpan? timeout = null)
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(30)
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryCount,
                Delay = retryDelay ?? TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = failureRatio,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = breakDuration ?? TimeSpan.FromSeconds(15)
            })
            .Build();
    }

    public static IHttpClientBuilder AddDefaultResiliencePolicy(this IHttpClientBuilder builder)
    {
        return builder.AddHttpMessageHandler(() => new ResilienceDelegatingHandler());
    }
}
