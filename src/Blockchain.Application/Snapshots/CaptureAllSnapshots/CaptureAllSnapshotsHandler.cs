using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.CaptureAllSnapshots;

public sealed class CaptureAllSnapshotsHandler(ISnapshotCaptureService capture)
    : ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<NetworkCaptureResult>>
{
    public async Task<IReadOnlyList<NetworkCaptureResult>> HandleAsync(
        CaptureAllSnapshotsCommand command, CancellationToken cancellationToken) =>
        await Task.WhenAll(BlockchainNetwork.SupportedNetworks.Select(chain => CaptureNetworkAsync(chain, cancellationToken)));

    private async Task<NetworkCaptureResult> CaptureNetworkAsync(BlockchainNetwork chain, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await capture.CaptureAsync(chain, cancellationToken);
            return new(chain.Coin, chain.Network, snapshot.Id, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ProviderException ex)
        {
            return new(chain.Coin, chain.Network, null, ex is ProviderCircuitOpenException ? "provider_circuit_open"
                : ex.TimedOut ? "provider_timeout" : "provider_unavailable");
        }
        // Persistence errors propagate: a database outage must not be disguised as a provider failure.
    }
}
