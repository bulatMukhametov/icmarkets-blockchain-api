namespace Blockchain.Api.Contracts;

public sealed record SnapshotHistoryResponse(IReadOnlyList<SnapshotResponse> Items, string? NextCursor);
