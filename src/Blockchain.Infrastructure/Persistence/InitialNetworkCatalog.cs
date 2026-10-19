using Blockchain.Domain;
using Blockchain.Infrastructure.BlockCypher;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.Infrastructure.Persistence;

// Initial exercise data only. Runtime discovery queries the database, never this list.
internal static class InitialNetworkCatalog
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        var bitcoin = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var ethereum = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var dash = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var litecoin = Guid.Parse("10000000-0000-0000-0000-000000000004");
        modelBuilder.Entity<Cryptocurrency>().HasData(
            new { Id = bitcoin, Code = "btc", Name = "Bitcoin" },
            new { Id = ethereum, Code = "eth", Name = "Ethereum" },
            new { Id = dash, Code = "dash", Name = "Dash" },
            new { Id = litecoin, Code = "ltc", Name = "Litecoin" });

        var networks = new[]
        {
            new { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), CryptocurrencyId = bitcoin, Code = "main", Kind = NetworkKind.Mainnet },
            new { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), CryptocurrencyId = bitcoin, Code = "test3", Kind = NetworkKind.Testnet },
            new { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), CryptocurrencyId = ethereum, Code = "main", Kind = NetworkKind.Mainnet },
            new { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), CryptocurrencyId = dash, Code = "main", Kind = NetworkKind.Mainnet },
            new { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), CryptocurrencyId = litecoin, Code = "main", Kind = NetworkKind.Mainnet }
        };
        modelBuilder.Entity<BlockchainNetwork>().HasData(networks);
        var providerCodes = new[] { "btc", "btc", "eth", "dash", "ltc" };
        modelBuilder.Entity<BlockCypherNetworkMapping>().HasData(networks.Select((network, index) => new
        {
            BlockchainNetworkId = network.Id, CurrencyCode = providerCodes[index], NetworkCode = network.Code
        }));
    }
}
