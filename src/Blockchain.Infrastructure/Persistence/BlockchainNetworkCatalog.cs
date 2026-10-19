using Blockchain.Application.Abstractions;
using Blockchain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.Infrastructure.Persistence;

public sealed class BlockchainNetworkCatalog(BlockchainDbContext db) : IBlockchainNetworkCatalog
{
    private IQueryable<BlockchainNetwork> Networks => db.Networks.AsNoTracking().Include(x => x.Cryptocurrency);

    public Task<BlockchainNetwork?> FindAsync(string currencyCode, string networkCode, CancellationToken cancellationToken)
    {
        var currency = currencyCode.ToLowerInvariant();
        var network = networkCode.ToLowerInvariant();
        return Networks.SingleOrDefaultAsync(x => x.Cryptocurrency.Code == currency && x.Code == network, cancellationToken);
    }

    public async Task<IReadOnlyList<BlockchainNetwork>> GetAllAsync(CancellationToken cancellationToken) =>
        await Networks.OrderBy(x => x.Cryptocurrency.Code).ThenBy(x => x.Code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BlockchainNetwork>> GetCollectableAsync(CancellationToken cancellationToken) =>
        await Networks.Where(x => db.BlockCypherMappings.Any(mapping => mapping.BlockchainNetworkId == x.Id))
            .OrderBy(x => x.Cryptocurrency.Code).ThenBy(x => x.Code).ToListAsync(cancellationToken);
}
