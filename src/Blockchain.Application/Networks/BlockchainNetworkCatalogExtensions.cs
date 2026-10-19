using Blockchain.Application.Validation;
using Blockchain.Domain;

namespace Blockchain.Application.Networks;

internal static class BlockchainNetworkCatalogExtensions
{
    public static async Task<BlockchainNetwork> GetRequiredAsync(this IBlockchainNetworkCatalog catalog,
        string currencyCode, string networkCode, CancellationToken cancellationToken) =>
        await catalog.FindAsync(currencyCode, networkCode, cancellationToken)
        ?? throw new RequestValidationException("chain", "Unsupported currency/network combination.");
}
