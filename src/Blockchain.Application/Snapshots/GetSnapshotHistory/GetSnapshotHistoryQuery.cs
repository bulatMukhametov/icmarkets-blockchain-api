using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.GetSnapshotHistory;

public sealed record GetSnapshotHistoryQuery(BlockchainNetwork Network, int PageSize = 20, string? Cursor = null);
