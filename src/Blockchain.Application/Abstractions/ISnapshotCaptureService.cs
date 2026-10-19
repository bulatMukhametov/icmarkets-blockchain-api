using Blockchain.Domain;

namespace Blockchain.Application.Abstractions;

public interface ISnapshotCaptureService
{
    Task<BlockchainNetworkSnapshot> CaptureAsync(BlockchainNetwork chain, CancellationToken cancellationToken);
}
