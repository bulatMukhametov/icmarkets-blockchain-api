using Blockchain.Domain;

namespace Blockchain.Application.Abstractions;

public interface IBlockchainSnapshotRepository
{
    // One append is one transaction. On completion, the entity has its database-generated ID.
    Task AddAndSaveAsync(BlockchainNetworkSnapshot snapshot, CancellationToken cancellationToken);
    Task<BlockchainNetworkSnapshot?> GetByIdAsync(BlockchainNetwork chain, long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<BlockchainNetworkSnapshot>> GetHistoryAsync(
        BlockchainNetwork chain, SnapshotCursor? before, int take, CancellationToken cancellationToken);
}
