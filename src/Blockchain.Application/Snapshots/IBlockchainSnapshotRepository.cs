using Blockchain.Domain;

namespace Blockchain.Application.Snapshots;

public interface IBlockchainSnapshotRepository
{
    // Persists a new snapshot in one transaction and assigns its database-generated ID.
    Task SaveAsync(BlockchainNetworkSnapshot snapshot, CancellationToken cancellationToken);
    Task<BlockchainNetworkSnapshot?> GetByIdAsync(BlockchainNetwork network, long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<BlockchainNetworkSnapshot>> GetHistoryAsync(
        BlockchainNetwork network, int skip, int take, CancellationToken cancellationToken);
}
