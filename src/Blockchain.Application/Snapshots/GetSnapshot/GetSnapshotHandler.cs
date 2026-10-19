using Blockchain.Application.Abstractions;
using Blockchain.Application.Networks;
using Blockchain.Application.Snapshots;
using FluentValidation;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.GetSnapshot;

public sealed class GetSnapshotHandler(IBlockchainSnapshotRepository repository, IBlockchainNetworkCatalog catalog,
    IValidator<GetSnapshotQuery> validator)
    : IQueryHandler<GetSnapshotQuery, BlockchainNetworkSnapshot?>
{
    public async Task<BlockchainNetworkSnapshot?> HandleAsync(GetSnapshotQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        var network = await catalog.GetRequiredAsync(query.CurrencyCode, query.NetworkCode, cancellationToken);
        return await repository.GetByIdAsync(network, query.Id, cancellationToken);
    }
}
