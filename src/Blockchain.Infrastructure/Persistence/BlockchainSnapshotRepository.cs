using Blockchain.Application.Snapshots;
using Blockchain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.Infrastructure.Persistence;

public sealed class BlockchainSnapshotRepository(BlockchainDbContext db) : IBlockchainSnapshotRepository
{
    public async Task SaveAsync(BlockchainNetworkSnapshot snapshot, CancellationToken cancellationToken)
    {
        // Catalog entities already exist. Insert only the snapshot, not its detached navigation graph.
        db.Entry(snapshot).State = EntityState.Added;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<BlockchainNetworkSnapshot?> GetByIdAsync(BlockchainNetwork network, long id, CancellationToken cancellationToken) =>
        Snapshots.SingleOrDefaultAsync(x => x.Id == id && x.BlockchainNetworkId == network.Id, cancellationToken);

    private IQueryable<BlockchainNetworkSnapshot> Snapshots => db.Snapshots.AsNoTracking()
        .Include(x => x.BlockchainNetwork).ThenInclude(x => x.Cryptocurrency);

    public async Task<IReadOnlyList<BlockchainNetworkSnapshot>> GetHistoryAsync(
        BlockchainNetwork network, int skip, int take, CancellationToken cancellationToken)
    {
        var query = Snapshots.Where(x => x.BlockchainNetworkId == network.Id);
        return await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);
    }
}
