namespace Blockchain.Application.Abstractions;

public sealed class ProviderCircuitOpenException(TimeSpan retryAfter, Exception? inner = null)
    : ProviderException("Provider circuit is open; retry later.", inner: inner)
{
    public TimeSpan RetryAfter { get; } = retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(1);
}
