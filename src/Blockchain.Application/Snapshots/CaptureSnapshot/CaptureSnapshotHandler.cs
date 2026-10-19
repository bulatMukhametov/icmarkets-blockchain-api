using Blockchain.Application.Abstractions;
using Blockchain.Application.Networks;
using Blockchain.Application.Providers;
using Blockchain.Application.Snapshots;
using Blockchain.Domain;
using FluentValidation;

namespace Blockchain.Application.Snapshots.CaptureSnapshot;

public sealed class CaptureSnapshotHandler(IBlockchainDataClient client, IBlockchainSnapshotRepository repository,
    IBlockchainNetworkCatalog catalog, IValidator<CaptureSnapshotCommand> validator)
    : ICommandHandler<CaptureSnapshotCommand, BlockchainNetworkSnapshot>
{
    public async Task<BlockchainNetworkSnapshot> HandleAsync(CaptureSnapshotCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var network = await catalog.GetRequiredAsync(command.CurrencyCode, command.NetworkCode, cancellationToken);
        var response = await client.FetchSnapshotAsync(network, cancellationToken);
        var snapshot = new BlockchainNetworkSnapshot(network, response.CreatedAt, response.RawResponseJson);
        await repository.SaveAsync(snapshot, cancellationToken);
        return snapshot;
    }
}
