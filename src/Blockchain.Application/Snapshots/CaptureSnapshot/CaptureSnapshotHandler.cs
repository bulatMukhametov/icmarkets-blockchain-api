using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Application.Snapshots.CaptureSnapshot;


public sealed class CaptureSnapshotHandler(IBlockchainDataClient client, IBlockchainSnapshotRepository repository)
    : ICommandHandler<CaptureSnapshotCommand, BlockchainNetworkSnapshot>
{
    public async Task<BlockchainNetworkSnapshot> HandleAsync(CaptureSnapshotCommand command, CancellationToken cancellationToken)
    {
        var response = await client.FetchSnapshotAsync(command.Network, cancellationToken);
        var snapshot = new BlockchainNetworkSnapshot(command.Network, response.RequestedAt, response.Payload);
        await repository.AddAndSaveAsync(snapshot, cancellationToken);
        return snapshot;
    }
}
