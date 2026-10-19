namespace Blockchain.Infrastructure.BlockCypher;

public interface IBlockCypherNetworkMappings
{
    Task<BlockCypherNetworkMapping?> FindAsync(Guid blockchainNetworkId, CancellationToken cancellationToken);
}
