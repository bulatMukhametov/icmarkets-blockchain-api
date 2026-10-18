using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.CaptureAllSnapshots;

public sealed record CaptureAllSnapshotsCommand;
public sealed record CaptureResult(string Coin, string Network, long? SnapshotId, string? Error)
{
    public bool Succeeded => SnapshotId.HasValue;
}

public sealed class CaptureAllSnapshotsHandler(ISnapshotCapture capture)
    : ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<CaptureResult>>
{
    public async Task<IReadOnlyList<CaptureResult>> HandleAsync(
        CaptureAllSnapshotsCommand command, CancellationToken cancellationToken) =>
        await Task.WhenAll(ChainId.Supported.Select(chain => CaptureOneAsync(chain, cancellationToken)));

    private async Task<CaptureResult> CaptureOneAsync(ChainId chain, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await capture.CaptureAsync(chain, cancellationToken);
            return new(chain.Coin, chain.Network, snapshot.Id, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ProviderException ex)
        {
            return new(chain.Coin, chain.Network, null, ex.TimedOut ? "provider_timeout" : "provider_unavailable");
        }
        // Persistence errors propagate: a database outage must not be disguised as a provider failure.
    }
}
