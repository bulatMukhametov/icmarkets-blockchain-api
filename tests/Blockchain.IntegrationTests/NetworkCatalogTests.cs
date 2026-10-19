using Blockchain.Domain;
using Blockchain.Infrastructure.BlockCypher;
using Blockchain.Infrastructure.Persistence;
using Blockchain.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Blockchain.IntegrationTests;

public sealed class NetworkCatalogTests
{
    [DatabaseFact]
    public async Task Upgrade_preserves_existing_snapshots_and_populates_network_foreign_keys()
    {
        await using var fixture = new FixtureScope();
        await fixture.Database.StartAsync("20261018192339_InitialSnapshots");
        await using var db = fixture.Database.CreateContext();
        var requestedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        const string payload = "{ \"large\":12345678901234567890, \"untouched\":true }";
        var id = 100L;
        foreach (var network in TestNetworkCatalog.Networks)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Snapshots\" (\"Id\", \"Coin\", \"Network\", \"CreatedAt\", \"Payload\") VALUES ({++id}, {network.Cryptocurrency.Code}, {network.Code}, {requestedAt}, {payload})");
        }
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        var snapshots = await db.Snapshots.Include(x => x.BlockchainNetwork).ThenInclude(x => x.Cryptocurrency).OrderBy(x => x.Id).ToArrayAsync();
        Assert.Equal(5, snapshots.Length);
        for (var index = 0; index < snapshots.Length; index++)
        {
            Assert.Equal(101 + index, snapshots[index].Id);
            Assert.Equal(TestNetworkCatalog.Networks[index].Id, snapshots[index].BlockchainNetworkId);
            Assert.Equal(payload, snapshots[index].RawResponseJson);
            Assert.Equal(requestedAt, snapshots[index].RequestedAtUtc);
        }
        Assert.Equal(4, await db.Cryptocurrencies.CountAsync());
        Assert.Equal(5, await db.BlockCypherMappings.CountAsync());
    }

    [DatabaseFact]
    public async Task Catalog_supports_unmapped_networks_and_enforces_uniqueness_and_history_references()
    {
        await using var fixture = new FixtureScope();
        await fixture.Database.StartAsync();
        await using var db = fixture.Database.CreateContext();
        var currency = new Cryptocurrency(Guid.NewGuid(), "doge", "Dogecoin");
        var network = new BlockchainNetwork(Guid.NewGuid(), currency, "production", NetworkKind.Mainnet);
        db.Networks.Add(network);
        await db.SaveChangesAsync();
        var catalog = new BlockchainNetworkCatalog(db);
        Assert.Equal(network.Id, (await catalog.FindAsync("DOGE", "PRODUCTION", default))!.Id);
        Assert.Contains(await catalog.GetAllAsync(default), x => x.Id == network.Id);
        Assert.DoesNotContain(await catalog.GetCollectableAsync(default), x => x.Id == network.Id);
        db.BlockCypherMappings.Add(new(network.Id, "doge", "main"));
        await db.SaveChangesAsync();
        Assert.Contains(await catalog.GetCollectableAsync(default), x => x.Id == network.Id);
        var snapshot = new BlockchainNetworkSnapshot(network, DateTimeOffset.UtcNow, "{}");
        await new BlockchainSnapshotRepository(db).AddAndSaveAsync(snapshot, default);
        db.BlockCypherMappings.Remove(await db.BlockCypherMappings.SingleAsync(x => x.BlockchainNetworkId == network.Id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.Networks.Remove(network);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Cryptocurrencies.Add(new(Guid.NewGuid(), "DOGE", "Duplicate"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Networks.Add(new(Guid.NewGuid(), currency, "production", NetworkKind.Testnet));
        db.Entry(currency).State = EntityState.Unchanged;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private sealed class FixtureScope : IAsyncDisposable
    {
        public PostgresFixture Database { get; } = new();
        public async ValueTask DisposeAsync() => await Database.DisposeAsync();
    }
}
