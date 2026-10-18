using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureSnapshot;
using Blockchain.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Blockchain.Infrastructure;

public sealed class ScopedSnapshotCapture(IServiceScopeFactory scopeFactory) : ISnapshotCapture
{
    public async Task<BlockchainSnapshot> CaptureAsync(ChainId chain, CancellationToken cancellationToken)
    {
        // Each concurrent capture owns a DbContext and disposes it independently.
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ICommandHandler<CaptureSnapshotCommand, BlockchainSnapshot>>()
            .HandleAsync(new(chain), cancellationToken);
    }
}
