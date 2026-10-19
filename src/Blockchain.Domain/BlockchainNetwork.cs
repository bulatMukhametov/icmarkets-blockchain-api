namespace Blockchain.Domain;

/// <summary>A cryptocurrency and its network, such as Bitcoin mainnet (btc/main).</summary>
public sealed record BlockchainNetwork
{
    /// <summary>Provider currency code: btc, eth, dash or ltc.</summary>
    public string Coin { get; }
    /// <summary>Network name: main for mainnet, or test3 for the supported Bitcoin testnet.</summary>
    public string Network { get; }

    private BlockchainNetwork(string coin, string network) => (Coin, Network) = (coin, network);

    public static IReadOnlyList<BlockchainNetwork> SupportedNetworks { get; } = Array.AsReadOnly(new[]
    {
        new BlockchainNetwork("eth", "main"), new BlockchainNetwork("dash", "main"),
        new BlockchainNetwork("btc", "main"), new BlockchainNetwork("btc", "test3"), new BlockchainNetwork("ltc", "main")
    });

    public static BlockchainNetwork? FindSupportedNetwork(string coin, string network) => SupportedNetworks.FirstOrDefault(x =>
        string.Equals(x.Coin, coin, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(x.Network, network, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => $"{Coin}/{Network}";
}
