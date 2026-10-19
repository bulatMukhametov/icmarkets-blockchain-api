using Blockchain.Application.Abstractions;
using Blockchain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.Infrastructure.Persistence;

public sealed class BlockchainSnapshotRepository(BlockchainDbContext db) : IBlockchainSnapshotRepository
{
    public async Task AddAndSaveAsync(BlockchainNetworkSnapshot snapshot, CancellationToken cancellationToken)
    {
        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<BlockchainNetworkSnapshot?> GetByIdAsync(BlockchainNetwork chain, long id, CancellationToken cancellationToken) =>
        db.Snapshots.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.Coin == chain.Coin && x.Network == chain.Network, cancellationToken);

    public async Task<IReadOnlyList<BlockchainNetworkSnapshot>> GetHistoryAsync(
        BlockchainNetwork chain, SnapshotCursor? before, int take, CancellationToken cancellationToken)
    {
        var query = db.Snapshots.AsNoTracking().Where(x => x.Coin == chain.Coin && x.Network == chain.Network);
        if (before is not null)
            query = query.Where(x => x.CreatedAt < before.CreatedAt ||
                (x.CreatedAt == before.CreatedAt && x.Id < before.Id));
        return await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(take).ToListAsync(cancellationToken);
    }
}
