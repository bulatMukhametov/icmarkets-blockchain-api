using Blockchain.Application.Abstractions;
using Blockchain.Application.Networks;
using Blockchain.Application.Providers;
using Blockchain.Application.Snapshots;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.CaptureAllSnapshots;

public sealed class CaptureAllSnapshotsHandler(ISnapshotCaptureService capture, IBlockchainNetworkCatalog catalog)
    : ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<NetworkCaptureResult>>
{
    public async Task<IReadOnlyList<NetworkCaptureResult>> HandleAsync(
        CaptureAllSnapshotsCommand command, CancellationToken cancellationToken)
    {
        var networks = await catalog.GetNetworksAvailableForCaptureAsync(cancellationToken);
        var tasks = networks.Select(network => CaptureNetworkAsync(network, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private async Task<NetworkCaptureResult> CaptureNetworkAsync(BlockchainNetwork network, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await capture.CaptureAsync(network, cancellationToken);
            return new(network.Cryptocurrency.Code, network.Code, snapshot.Id, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ProviderException ex)
        {
            return new(network.Cryptocurrency.Code, network.Code, null, ex is ProviderCircuitOpenException
                ? SnapshotCaptureFailure.ProviderCircuitOpen
                : ex.TimedOut ? SnapshotCaptureFailure.ProviderTimeout : SnapshotCaptureFailure.ProviderUnavailable);
        }
        // Persistence errors propagate: a database outage must not be disguised as a provider failure.
    }
}
