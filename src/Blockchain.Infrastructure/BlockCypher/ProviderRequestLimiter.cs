using Blockchain.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Blockchain.Infrastructure.BlockCypher;

// Singleton: every attempt, including retries, consumes the same per-process budget.
public sealed class ProviderRequestLimiter(IOptions<BlockCypherOptions> options, TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim mutex = new(1, 1);
    private readonly Queue<DateTimeOffset> requests = new();
    private readonly object cooldownLock = new();
    private DateTimeOffset cooldownUntil;
    private DateTimeOffset nextRequest;

    public void ApplyCooldown(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return;
        lock (cooldownLock)
        {
            var now = clock.GetUtcNow();
            var until = duration >= DateTimeOffset.MaxValue - now ? DateTimeOffset.MaxValue : now + duration;
            if (until > cooldownUntil) cooldownUntil = until;
        }
    }

    private void ThrowIfCoolingDown()
    {
        if (clock.GetUtcNow() < cooldownUntil)
            throw new ProviderException("Provider requested a shared cooldown; retry later.");
    }

    public async Task WaitForPermitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (cooldownLock) ThrowIfCoolingDown();
        await mutex.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            lock (cooldownLock) ThrowIfCoolingDown();
            var delay = nextRequest - now;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, clock, cancellationToken);
            lock (cooldownLock)
            {
                // A 429 may arrive from an in-flight request while this caller waits for spacing.
                ThrowIfCoolingDown();
                now = clock.GetUtcNow();
                while (requests.TryPeek(out var oldest) && oldest <= now.AddHours(-1)) requests.Dequeue();
                if (requests.Count >= options.Value.RequestsPerHour)
                    throw new ProviderException("Local provider request budget exhausted.");
                requests.Enqueue(now);
                nextRequest = now.AddMilliseconds(1000d / options.Value.RequestsPerSecond + 1);
            }
        }
        finally { mutex.Release(); }
    }

    public void Dispose() => mutex.Dispose();
}
