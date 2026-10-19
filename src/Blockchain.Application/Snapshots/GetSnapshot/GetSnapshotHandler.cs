using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.GetSnapshot;

public sealed class GetSnapshotHandler(IBlockchainSnapshotRepository repository)
    : IQueryHandler<GetSnapshotQuery, BlockchainNetworkSnapshot?>
{
    public Task<BlockchainNetworkSnapshot?> HandleAsync(GetSnapshotQuery query, CancellationToken cancellationToken) =>
        repository.GetByIdAsync(query.Network, query.Id, cancellationToken);
}
