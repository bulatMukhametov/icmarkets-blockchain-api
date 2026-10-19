using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.GetSnapshot;

public sealed record GetSnapshotQuery(BlockchainNetwork Network, long Id);
