using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.GetSnapshotHistory;

public sealed record SnapshotHistoryPage(IReadOnlyList<BlockchainNetworkSnapshot> Items, int Page, int PageSize, bool HasNextPage);
