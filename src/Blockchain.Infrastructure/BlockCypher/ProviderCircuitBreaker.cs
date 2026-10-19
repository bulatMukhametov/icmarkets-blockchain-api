using System.Net;
using Blockchain.Application.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

namespace Blockchain.Infrastructure.BlockCypher;

// One instance per provider/process, shared by every typed client and currency.
public sealed class ProviderCircuitBreaker
{
    private readonly ResiliencePipeline<ProviderAttemptResult> pipeline;
    private readonly CircuitBreakerStateProvider state = new();
    private readonly TimeProvider clock;
    private long retryAtTicks;

    public ProviderCircuitBreaker(IOptions<BlockCypherOptions> options, TimeProvider clock,
        ILogger<ProviderCircuitBreaker> logger)
    {
        this.clock = clock;
        var settings = options.Value;
        pipeline = new ResiliencePipelineBuilder<ProviderAttemptResult> { TimeProvider = clock }
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<ProviderAttemptResult>
            {
                FailureRatio = settings.CircuitBreakerFailureRatio,
                MinimumThroughput = settings.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(settings.CircuitBreakerSamplingSeconds),
                BreakDuration = TimeSpan.FromSeconds(settings.CircuitBreakerBreakSeconds),
                StateProvider = state,
                ShouldHandle = args => new ValueTask<bool>(args.Outcome.Exception switch
                {
                    HttpRequestException or IOException or ProviderAttemptTimeoutException => true,
                    // A cancelled probe provides no evidence of recovery. Release it by reopening;
                    // cancellation during normal operation never trips the closed circuit.
                    OperationCanceledException => state.CircuitState == CircuitState.HalfOpen,
                    null => args.Outcome.Result.StatusCode == HttpStatusCode.RequestTimeout ||
                        (int)args.Outcome.Result.StatusCode >= 500,
                    _ => false
                }),
                OnOpened = args =>
                {
                    Interlocked.Exchange(ref retryAtTicks, clock.GetUtcNow().Add(args.BreakDuration).UtcTicks);
                    logger.LogWarning("BlockCypher circuit opened for {BreakSeconds} seconds", args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    logger.LogInformation("BlockCypher circuit allows one recovery probe");
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("BlockCypher circuit closed");
                    return ValueTask.CompletedTask;
                }
            }).Build();
    }

    // Fast rejection before waiting for or consuming a local quota slot. ExecuteAsync below
    // makes the authoritative admission decision again after the gate, including concurrent probes.
    internal void ThrowIfUnavailable(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var remaining = new DateTimeOffset(Interlocked.Read(ref retryAtTicks), TimeSpan.Zero) - clock.GetUtcNow();
        if (state.CircuitState == CircuitState.HalfOpen ||
            (state.CircuitState == CircuitState.Open && remaining > TimeSpan.Zero))
            throw new ProviderCircuitOpenException(remaining);
    }

    internal async ValueTask<ProviderAttemptResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<ProviderAttemptResult>> action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return await pipeline.ExecuteAsync(action, token); }
        catch (BrokenCircuitException ex)
        {
            throw new ProviderCircuitOpenException(ex.RetryAfter ?? TimeSpan.FromSeconds(1), ex);
        }
    }
}

internal readonly record struct ProviderAttemptResult(HttpStatusCode StatusCode, string? Payload, TimeSpan? RetryAfter);
internal sealed class ProviderAttemptTimeoutException(Exception inner) : TimeoutException("Provider attempt timed out.", inner);
