using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.GetSnapshot;

public sealed record GetSnapshotQuery(ChainId Chain, long Id);
public sealed class GetSnapshotHandler(IBlockchainSnapshotRepository repository)
    : IQueryHandler<GetSnapshotQuery, BlockchainSnapshot?>
{
    public Task<BlockchainSnapshot?> HandleAsync(GetSnapshotQuery query, CancellationToken cancellationToken) =>
        repository.GetByIdAsync(query.Chain, query.Id, cancellationToken);
}
