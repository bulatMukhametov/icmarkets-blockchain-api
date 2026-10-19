using Blockchain.Application.Abstractions;
using Blockchain.Domain;

namespace Blockchain.Tests.Shared;

public sealed class TestNetworkCatalog : IBlockchainNetworkCatalog
{
    public static IReadOnlyList<BlockchainNetwork> Networks { get; } = CreateNetworks();
    public IReadOnlyList<BlockchainNetwork> Entries { get; init; } = Networks;

    public static BlockchainNetwork? Find(string currency, string network) => Networks.FirstOrDefault(x =>
        string.Equals(x.Cryptocurrency.Code, currency, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(x.Code, network, StringComparison.OrdinalIgnoreCase));

    public Task<BlockchainNetwork?> FindAsync(string currencyCode, string networkCode, CancellationToken token) =>
        Task.FromResult(Entries.FirstOrDefault(x =>
            string.Equals(x.Cryptocurrency.Code, currencyCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Code, networkCode, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<BlockchainNetwork>> GetAllAsync(CancellationToken token) => Task.FromResult(Entries);
    public Task<IReadOnlyList<BlockchainNetwork>> GetCollectableAsync(CancellationToken token) => Task.FromResult(Entries);

    private static IReadOnlyList<BlockchainNetwork> CreateNetworks()
    {
        var btc = new Cryptocurrency(Guid.Parse("10000000-0000-0000-0000-000000000001"), "btc", "Bitcoin");
        var eth = new Cryptocurrency(Guid.Parse("10000000-0000-0000-0000-000000000002"), "eth", "Ethereum");
        var dash = new Cryptocurrency(Guid.Parse("10000000-0000-0000-0000-000000000003"), "dash", "Dash");
        var ltc = new Cryptocurrency(Guid.Parse("10000000-0000-0000-0000-000000000004"), "ltc", "Litecoin");
        return new[]
        {
            new BlockchainNetwork(Guid.Parse("20000000-0000-0000-0000-000000000001"), btc, "main", NetworkKind.Mainnet),
            new BlockchainNetwork(Guid.Parse("20000000-0000-0000-0000-000000000002"), btc, "test3", NetworkKind.Testnet),
            new BlockchainNetwork(Guid.Parse("20000000-0000-0000-0000-000000000003"), eth, "main", NetworkKind.Mainnet),
            new BlockchainNetwork(Guid.Parse("20000000-0000-0000-0000-000000000004"), dash, "main", NetworkKind.Mainnet),
            new BlockchainNetwork(Guid.Parse("20000000-0000-0000-0000-000000000005"), ltc, "main", NetworkKind.Mainnet)
        };
    }
}
