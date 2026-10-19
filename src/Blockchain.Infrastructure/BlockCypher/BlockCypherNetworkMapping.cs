namespace Blockchain.Infrastructure.BlockCypher;

/// <summary>Provider-specific addressing for a catalog network; never a user-supplied URL.</summary>
public sealed class BlockCypherNetworkMapping
{
    public Guid BlockchainNetworkId { get; private set; }
    public string CurrencyCode { get; private set; } = null!;
    public string NetworkCode { get; private set; } = null!;
    public string RelativePath => $"{CurrencyCode}/{NetworkCode}";
    public string ExpectedResponseName => $"{CurrencyCode}.{NetworkCode}";

    private BlockCypherNetworkMapping() { }

    public BlockCypherNetworkMapping(Guid blockchainNetworkId, string currencyCode, string networkCode)
    {
        if (blockchainNetworkId == Guid.Empty) throw new ArgumentException("Network ID must not be empty.", nameof(blockchainNetworkId));
        BlockchainNetworkId = blockchainNetworkId;
        CurrencyCode = NormalizeSegment(currencyCode);
        NetworkCode = NormalizeSegment(networkCode);
    }

    private static string NormalizeSegment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 32 || !normalized.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException("Provider path segments must contain 1..32 ASCII letters or digits.", nameof(value));
        return normalized;
    }
}
