namespace Blockchain.Domain;

public sealed class BlockchainSnapshot
{
    public long Id { get; private set; }
    public string Coin { get; private set; } = null!;
    public string Network { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public string Payload { get; private set; } = null!;

    private BlockchainSnapshot() { } // EF Core materialization.

    public BlockchainSnapshot(ChainId chain, DateTimeOffset createdAt, string payload)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        if (createdAt == default) throw new ArgumentOutOfRangeException(nameof(createdAt));
        Coin = chain.Coin;
        Network = chain.Network;
        // PostgreSQL stores microseconds. Normalize before POST responses and cursor creation.
        var utc = createdAt.ToUniversalTime();
        CreatedAt = new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
        Payload = payload;
    }
}
