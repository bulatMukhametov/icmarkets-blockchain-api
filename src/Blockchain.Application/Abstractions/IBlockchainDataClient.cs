using Blockchain.Domain;

namespace Blockchain.Application.Abstractions;

public interface IBlockchainDataClient
{
    Task<ProviderSnapshot> FetchSnapshotAsync(BlockchainNetwork chain, CancellationToken cancellationToken);
}
