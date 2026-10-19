using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.CaptureSnapshot;

public sealed record CaptureSnapshotCommand(BlockchainNetwork Network);
