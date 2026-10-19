namespace Blockchain.Domain;

/// <summary>A saved provider response describing one blockchain network at the time it was requested.</summary>
public sealed class BlockchainNetworkSnapshot
{
    public long Id { get; private set; }
    public string Coin { get; private set; } = null!;
    public string Network { get; private set; } = null!;
    /// <summary>UTC time our first provider request started, not a block timestamp or database insert time.</summary>
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>The complete original JSON response; this is a network summary, not a copy of the blockchain.</summary>
    public string Payload { get; private set; } = null!;

    private BlockchainNetworkSnapshot() { } // EF Core materialization.

    public BlockchainNetworkSnapshot(BlockchainNetwork blockchainNetwork, DateTimeOffset createdAt, string payload)
    {
        ArgumentNullException.ThrowIfNull(blockchainNetwork);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        if (createdAt == default) throw new ArgumentOutOfRangeException(nameof(createdAt));
        Coin = blockchainNetwork.Coin;
        Network = blockchainNetwork.Network;
        // PostgreSQL stores microseconds. Normalize before POST responses and cursor creation.
        var utc = createdAt.ToUniversalTime();
        CreatedAt = new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
        Payload = payload;
    }
}
