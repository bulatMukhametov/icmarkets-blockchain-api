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
                ShouldHandle = args => new ValueTask<bool>(ShouldRetry(args.Outcome)),
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
        retries.ExecuteAsync(attemptToken => ExecuteAttemptAsync(send, attemptToken), token);

    private async ValueTask<ProviderAttemptResult> ExecuteAttemptAsync(
        Func<CancellationToken, ValueTask<ProviderAttemptResult>> send, CancellationToken cancellationToken)
    {
        circuit.ThrowIfUnavailable(cancellationToken);
        await limiter.WaitForPermitAsync(cancellationToken);
        return await circuit.ExecuteAsync(send, cancellationToken);
    }

    private static bool ShouldRetry(Outcome<ProviderAttemptResult> outcome) => outcome.Exception switch
    {
        HttpRequestException or IOException or ProviderAttemptTimeoutException => true,
        null => outcome.Result.StatusCode == HttpStatusCode.RequestTimeout || (int)outcome.Result.StatusCode >= 500,
        _ => false
    };
}
