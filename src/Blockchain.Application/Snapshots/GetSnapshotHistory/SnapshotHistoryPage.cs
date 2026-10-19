using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.GetSnapshotHistory;

public sealed record SnapshotHistoryPage(IReadOnlyList<BlockchainNetworkSnapshot> Items, string? NextCursor);
