using Blockchain.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Blockchain.Infrastructure.BlockCypher;

// Singleton: every attempt, including retries, consumes the same per-process budget.
public sealed class ProviderRequestGate(IOptions<BlockCypherOptions> options, TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim mutex = new(1, 1);
    private readonly Queue<DateTimeOffset> requests = new();
    private DateTimeOffset nextRequest;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            var delay = nextRequest - now;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, clock, cancellationToken);
            now = clock.GetUtcNow();
            while (requests.TryPeek(out var oldest) && oldest <= now.AddHours(-1)) requests.Dequeue();
            if (requests.Count >= options.Value.RequestsPerHour)
                throw new ProviderException("Local provider request budget exhausted.");
            requests.Enqueue(now);
            nextRequest = now.AddMilliseconds(1000d / options.Value.RequestsPerSecond + 1);
        }
        finally { mutex.Release(); }
    }

    public void Dispose() => mutex.Dispose();
}
