namespace Blockchain.Domain;

/// <summary>A saved provider response describing one blockchain network at the time it was requested.</summary>
public sealed class BlockchainNetworkSnapshot
{
    public long Id { get; private set; }
    public Guid BlockchainNetworkId { get; private set; }
    public BlockchainNetwork BlockchainNetwork { get; private set; } = null!;
    /// <summary>UTC time Application started fetching data, before limiter waiting and retries; not database insert time.</summary>
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>The complete original JSON response; this is a network summary, not a copy of the blockchain.</summary>
    public string RawResponseJson { get; private set; } = null!;

    private BlockchainNetworkSnapshot() { } // EF Core materialization.

    public BlockchainNetworkSnapshot(BlockchainNetwork blockchainNetwork, DateTimeOffset createdAt, string rawResponseJson)
    {
        ArgumentNullException.ThrowIfNull(blockchainNetwork);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawResponseJson);
        if (createdAt == default) throw new ArgumentOutOfRangeException(nameof(createdAt));
        BlockchainNetworkId = blockchainNetwork.Id;
        BlockchainNetwork = blockchainNetwork;
        // PostgreSQL stores microseconds. Normalize before returning the created snapshot.
        var utc = createdAt.ToUniversalTime();
        CreatedAt = new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
        RawResponseJson = rawResponseJson;
    }
}
