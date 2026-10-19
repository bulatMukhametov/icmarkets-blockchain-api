using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots;
using Blockchain.Application.Snapshots.CaptureSnapshot;
using Blockchain.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Blockchain.Infrastructure.Collection;

public sealed class ScopedSnapshotCaptureService(IServiceScopeFactory scopeFactory) : ISnapshotCaptureService
{
    public async Task<BlockchainNetworkSnapshot> CaptureAsync(BlockchainNetwork network, CancellationToken cancellationToken)
    {
        // Each concurrent capture owns a DbContext and disposes it independently.
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ICommandHandler<CaptureSnapshotCommand, BlockchainNetworkSnapshot>>()
            .HandleAsync(new(network.Cryptocurrency.Code, network.Code), cancellationToken);
    }
}
