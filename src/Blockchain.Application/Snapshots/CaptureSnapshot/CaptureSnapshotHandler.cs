using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.CaptureSnapshot;

public sealed record CaptureSnapshotCommand(ChainId Chain);

public sealed class CaptureSnapshotHandler(IBlockchainDataClient client, IBlockchainSnapshotRepository repository)
    : ICommandHandler<CaptureSnapshotCommand, BlockchainSnapshot>
{
    public async Task<BlockchainSnapshot> HandleAsync(CaptureSnapshotCommand command, CancellationToken cancellationToken)
    {
        var response = await client.FetchAsync(command.Chain, cancellationToken);
        var snapshot = new BlockchainSnapshot(command.Chain, response.RequestedAt, response.Payload);
        await repository.AddAsync(snapshot, cancellationToken);
        return snapshot;
    }
}
