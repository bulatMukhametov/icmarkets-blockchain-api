using Blockchain.Application.Providers;
using Microsoft.Extensions.Options;

namespace Blockchain.Infrastructure.BlockCypher;

// Singleton: every attempt, including retries, consumes the same per-process budget.
public sealed class ProviderRequestLimiter(IOptions<BlockCypherOptions> options, TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim admissionLock = new(1, 1);
    private readonly Queue<DateTimeOffset> requestTimes = new();
    private DateTimeOffset nextRequestAt;

    public async Task WaitForPermitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await admissionLock.WaitAsync(cancellationToken);
        try
        {
            await WaitForRequestSpacingAsync(cancellationToken);
            ReserveRequestQuota();
        }
        finally { admissionLock.Release(); }
    }

    private async Task WaitForRequestSpacingAsync(CancellationToken cancellationToken)
    {
        var delay = nextRequestAt - clock.GetUtcNow();
        if (delay > TimeSpan.Zero) await Task.Delay(delay, clock, cancellationToken);
    }

    // Called while holding admissionLock.
    private void ReserveRequestQuota()
    {
        var now = clock.GetUtcNow();
        var windowStart = now.AddHours(-1);
        while (requestTimes.TryPeek(out var oldest) && oldest <= windowStart) requestTimes.Dequeue();
        if (requestTimes.Count >= options.Value.RequestsPerHour)
            throw new ProviderException("Local provider request budget exhausted.");
        requestTimes.Enqueue(now);
        nextRequestAt = now.AddMilliseconds(1000d / options.Value.RequestsPerSecond + 1);
    }

    public void Dispose() => admissionLock.Dispose();
}
