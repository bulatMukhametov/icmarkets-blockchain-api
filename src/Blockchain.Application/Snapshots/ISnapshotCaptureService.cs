using Blockchain.Domain;

namespace Blockchain.Application.Snapshots;

public interface ISnapshotCaptureService
{
    Task<BlockchainNetworkSnapshot> CaptureAsync(BlockchainNetwork network, CancellationToken cancellationToken);
}
