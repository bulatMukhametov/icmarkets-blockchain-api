namespace Blockchain.Api.Contracts;

public sealed record SnapshotHistoryResponse(IReadOnlyList<SnapshotResponse> Items, int Page, int PageSize, bool HasNextPage);
