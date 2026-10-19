using Blockchain.Infrastructure.BlockCypher;

namespace Blockchain.Tests.Shared;

public sealed class TestBlockCypherMappings : IBlockCypherNetworkMappings
{
    public Dictionary<Guid, BlockCypherNetworkMapping> Entries { get; } = TestNetworkCatalog.Networks
        .ToDictionary(x => x.Id, x => new BlockCypherNetworkMapping(x.Id, x.Cryptocurrency.Code, x.Code));

    public Task<BlockCypherNetworkMapping?> FindAsync(Guid blockchainNetworkId, CancellationToken token) =>
        Task.FromResult(Entries.GetValueOrDefault(blockchainNetworkId));
}
