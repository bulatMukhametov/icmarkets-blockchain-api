using Blockchain.Application.Abstractions;
using Blockchain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.Infrastructure.Persistence;

public sealed class BlockchainSnapshotRepository(BlockchainDbContext db) : IBlockchainSnapshotRepository
{
    public async Task AddAndSaveAsync(BlockchainNetworkSnapshot snapshot, CancellationToken cancellationToken)
    {
        // Catalog entities already exist. Insert only the snapshot, not its detached navigation graph.
        db.Entry(snapshot).State = EntityState.Added;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<BlockchainNetworkSnapshot?> GetByIdAsync(BlockchainNetwork chain, long id, CancellationToken cancellationToken) =>
        Snapshots.SingleOrDefaultAsync(x => x.Id == id && x.BlockchainNetworkId == chain.Id, cancellationToken);

    private IQueryable<BlockchainNetworkSnapshot> Snapshots => db.Snapshots.AsNoTracking()
        .Include(x => x.BlockchainNetwork).ThenInclude(x => x.Cryptocurrency);

    public async Task<IReadOnlyList<BlockchainNetworkSnapshot>> GetHistoryAsync(
        BlockchainNetwork chain, SnapshotCursor? before, int take, CancellationToken cancellationToken)
    {
        var query = Snapshots.Where(x => x.BlockchainNetworkId == chain.Id);
        if (before is not null)
            query = query.Where(x => x.RequestedAtUtc < before.CreatedAt ||
                (x.RequestedAtUtc == before.CreatedAt && x.Id < before.Id));
        return await query.OrderByDescending(x => x.RequestedAtUtc).ThenByDescending(x => x.Id)
            .Take(take).ToListAsync(cancellationToken);
    }
}
