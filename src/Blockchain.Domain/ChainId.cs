namespace Blockchain.Domain;

public sealed record ChainId
{
    public string Coin { get; }
    public string Network { get; }

    private ChainId(string coin, string network) => (Coin, Network) = (coin, network);

    public static IReadOnlyList<ChainId> Supported { get; } = Array.AsReadOnly(new[]
    {
        new ChainId("eth", "main"), new ChainId("dash", "main"),
        new ChainId("btc", "main"), new ChainId("btc", "test3"), new ChainId("ltc", "main")
    });

    public static ChainId? Find(string coin, string network) => Supported.FirstOrDefault(x =>
        string.Equals(x.Coin, coin, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(x.Network, network, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => $"{Coin}/{Network}";
}
