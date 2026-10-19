using Blockchain.Domain;

namespace Blockchain.Application.Networks;

public interface IBlockchainNetworkCatalog
{
    Task<BlockchainNetwork?> FindAsync(string currencyCode, string networkCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<BlockchainNetwork>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BlockchainNetwork>> GetNetworksAvailableForCaptureAsync(CancellationToken cancellationToken);
}
