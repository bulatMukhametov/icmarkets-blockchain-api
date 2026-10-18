using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.GetSnapshotHistory;

public sealed record GetSnapshotHistoryQuery(ChainId Chain, int PageSize = 20, string? Cursor = null);
public sealed record SnapshotPage(IReadOnlyList<BlockchainSnapshot> Items, string? NextCursor);

public sealed class GetSnapshotHistoryHandler(IBlockchainSnapshotRepository repository)
    : IQueryHandler<GetSnapshotHistoryQuery, SnapshotPage>
{
    public async Task<SnapshotPage> HandleAsync(GetSnapshotHistoryQuery query, CancellationToken cancellationToken)
    {
        if (query.PageSize is < 1 or > 100)
            throw new RequestValidationException("pageSize", "Page size must be between 1 and 100.");
        var cursor = SnapshotCursor.Decode(query.Cursor, query.Chain);
        var rows = await repository.GetHistoryAsync(query.Chain, cursor, query.PageSize + 1, cancellationToken);
        var items = rows.Take(query.PageSize).ToArray();
        return new(items, rows.Count > query.PageSize ? SnapshotCursor.Encode(items[^1]) : null);
    }
}
