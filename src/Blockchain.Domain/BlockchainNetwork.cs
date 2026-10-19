namespace Blockchain.Domain;

/// <summary>A specific network belonging to a currency, with an identity independent of its provider.</summary>
public sealed class BlockchainNetwork
{
    public Guid Id { get; private set; }
    public Guid CryptocurrencyId { get; private set; }
    public Cryptocurrency Cryptocurrency { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public NetworkKind Kind { get; private set; }

    private BlockchainNetwork() { }

    public BlockchainNetwork(Guid id, Cryptocurrency cryptocurrency, string code, NetworkKind kind)
    {
        if (id == Guid.Empty) throw new ArgumentException("Network ID must not be empty.", nameof(id));
        ArgumentNullException.ThrowIfNull(cryptocurrency);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Id = id;
        CryptocurrencyId = cryptocurrency.Id;
        Cryptocurrency = cryptocurrency;
        Code = CatalogCode.Normalize(code);
        Kind = kind;
    }

    public override string ToString() => $"{Cryptocurrency.Code}/{Code}";
}
