using Blockchain.Infrastructure.BlockCypher;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.Infrastructure.Persistence;

public sealed class BlockCypherNetworkMappings(BlockchainDbContext db) : IBlockCypherNetworkMappings
{
    public Task<BlockCypherNetworkMapping?> FindAsync(Guid blockchainNetworkId, CancellationToken cancellationToken) =>
        db.BlockCypherMappings.AsNoTracking().SingleOrDefaultAsync(x => x.BlockchainNetworkId == blockchainNetworkId, cancellationToken);
}
