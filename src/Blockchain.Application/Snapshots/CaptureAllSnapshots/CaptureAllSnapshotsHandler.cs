using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.CaptureAllSnapshots;

public sealed class CaptureAllSnapshotsHandler(ISnapshotCaptureService capture, IBlockchainNetworkCatalog catalog)
    : ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<NetworkCaptureResult>>
{
    public async Task<IReadOnlyList<NetworkCaptureResult>> HandleAsync(
        CaptureAllSnapshotsCommand command, CancellationToken cancellationToken)
    {
        var networks = await catalog.GetCollectableAsync(cancellationToken);
        var results = new NetworkCaptureResult[networks.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, networks.Count), new ParallelOptions
        {
            MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken
        }, async (index, token) => results[index] = await CaptureNetworkAsync(networks[index], token));
        return results;
    }

    private async Task<NetworkCaptureResult> CaptureNetworkAsync(BlockchainNetwork chain, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await capture.CaptureAsync(chain, cancellationToken);
            return new(chain.Cryptocurrency.Code, chain.Code, snapshot.Id, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ProviderException ex)
        {
            return new(chain.Cryptocurrency.Code, chain.Code, null, ex is ProviderCircuitOpenException ? "provider_circuit_open"
                : ex.TimedOut ? "provider_timeout" : "provider_unavailable");
        }
        // Persistence errors propagate: a database outage must not be disguised as a provider failure.
    }
}
