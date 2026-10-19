using Blockchain.Domain;

namespace Blockchain.Application.Providers;

public interface IBlockchainDataClient
{
    Task<ProviderSnapshot> FetchSnapshotAsync(BlockchainNetwork network, CancellationToken cancellationToken);
}
