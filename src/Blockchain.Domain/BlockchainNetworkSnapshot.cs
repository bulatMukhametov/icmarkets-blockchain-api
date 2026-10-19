namespace Blockchain.Domain;

/// <summary>A saved provider response describing one blockchain network at the time it was requested.</summary>
public sealed class BlockchainNetworkSnapshot
{
    public long Id { get; private set; }
    public Guid BlockchainNetworkId { get; private set; }
    public BlockchainNetwork BlockchainNetwork { get; private set; } = null!;
    /// <summary>UTC time our first provider request started, not a block timestamp or database insert time.</summary>
    public DateTimeOffset RequestedAtUtc { get; private set; }
    /// <summary>The complete original JSON response; this is a network summary, not a copy of the blockchain.</summary>
    public string RawResponseJson { get; private set; } = null!;

    private BlockchainNetworkSnapshot() { } // EF Core materialization.

    public BlockchainNetworkSnapshot(BlockchainNetwork blockchainNetwork, DateTimeOffset requestedAt, string rawResponseJson)
    {
        ArgumentNullException.ThrowIfNull(blockchainNetwork);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawResponseJson);
        if (requestedAt == default) throw new ArgumentOutOfRangeException(nameof(requestedAt));
        BlockchainNetworkId = blockchainNetwork.Id;
        BlockchainNetwork = blockchainNetwork;
        // PostgreSQL stores microseconds. Normalize before POST responses and cursor creation.
        var utc = requestedAt.ToUniversalTime();
        RequestedAtUtc = new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
        RawResponseJson = rawResponseJson;
    }
}
