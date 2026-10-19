using Blockchain.Application.Abstractions;
using Blockchain.Application.Networks;
using Blockchain.Application.Snapshots;
using FluentValidation;

namespace Blockchain.Application.Snapshots.GetSnapshotHistory;

public sealed class GetSnapshotHistoryHandler(IBlockchainSnapshotRepository repository, IBlockchainNetworkCatalog catalog,
    IValidator<GetSnapshotHistoryQuery> validator)
    : IQueryHandler<GetSnapshotHistoryQuery, SnapshotHistoryPage>
{
    public async Task<SnapshotHistoryPage> HandleAsync(GetSnapshotHistoryQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        var skip = (query.Page - 1) * query.PageSize;
        var network = await catalog.GetRequiredAsync(query.CurrencyCode, query.NetworkCode, cancellationToken);
        var rows = await repository.GetHistoryAsync(network, skip, query.PageSize + 1, cancellationToken);
        var items = rows.Take(query.PageSize).ToArray();
        return new(items, query.Page, query.PageSize, rows.Count > query.PageSize);
    }
}
