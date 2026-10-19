using System.Net;
using Polly;
using Polly.Retry;

namespace Blockchain.Infrastructure.BlockCypher;

internal sealed class ProviderResiliencePipeline
{
    private readonly ResiliencePipeline<ProviderAttemptResult> retries;
    private readonly ProviderRequestLimiter limiter;
    private readonly ProviderCircuitBreaker circuit;

    public ProviderResiliencePipeline(BlockCypherOptions settings, TimeProvider clock,
        ProviderRequestLimiter limiter, ProviderCircuitBreaker circuit)
    {
        this.limiter = limiter;
        this.circuit = circuit;
        var builder = new ResiliencePipelineBuilder<ProviderAttemptResult> { TimeProvider = clock };
        if (settings.MaxRetries > 0)
        {
            builder.AddRetry(new RetryStrategyOptions<ProviderAttemptResult>
            {
                MaxRetryAttempts = settings.MaxRetries,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = args => new ValueTask<bool>(args.Outcome.Exception switch
                {
                    HttpRequestException or IOException or ProviderAttemptTimeoutException => true,
                    null => args.Outcome.Result.StatusCode == HttpStatusCode.RequestTimeout ||
                        (int)args.Outcome.Result.StatusCode >= 500,
                    _ => false
                }),
                OnRetry = args =>
                {
                    // Do not wait for another attempt if this failure opened the shared circuit.
                    circuit.ThrowIfUnavailable(args.Context.CancellationToken);
                    return ValueTask.CompletedTask;
                }
            });
        }
        retries = builder.Build();
    }

    public ValueTask<ProviderAttemptResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<ProviderAttemptResult>> send, CancellationToken token) =>
        retries.ExecuteAsync(async attemptToken =>
        {
            circuit.ThrowIfUnavailable(attemptToken);
            await limiter.WaitForPermitAsync(attemptToken);
            return await circuit.ExecuteAsync(send, attemptToken);
        }, token);
}
