using Blockchain.Application.Snapshots.GetSnapshotHistory;
using Blockchain.Application.Validation;
using Blockchain.Domain;
using Blockchain.Infrastructure.Persistence;
using Blockchain.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.IntegrationTests;

public sealed class PersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Migrations_payload_preservation_network_isolation_and_stable_pagination()
    {
        await postgres.StartAsync();
        await using var db = postgres.CreateContext();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var repository = new BlockchainSnapshotRepository(db);
        var main = Blockchain.Tests.Shared.TestNetworkCatalog.Find("btc", "main")!;
        var testnet = Blockchain.Tests.Shared.TestNetworkCatalog.Find("btc", "test3")!;
        var time = DateTimeOffset.UtcNow.AddTicks(17);
        const string raw = "{ \"name\": \"BTC.main\", \"large\": 12345678901234567890, \"extra\": [true,null] }";
        var old = new BlockchainNetworkSnapshot(main, time.AddMinutes(-1), raw);
        var first = new BlockchainNetworkSnapshot(main, time, raw);
        var second = new BlockchainNetworkSnapshot(main, time, raw);
        await repository.SaveAsync(old, default);
        await repository.SaveAsync(first, default);
        await repository.SaveAsync(second, default);
        await repository.SaveAsync(new(testnet, time.AddHours(1), "{}"), default);
        db.ChangeTracker.Clear();

        var handler = new GetSnapshotHistoryHandler(repository, new BlockchainNetworkCatalog(db), new GetSnapshotHistoryQueryValidator());
        var page1 = await handler.HandleAsync(new("btc", "main", 1, 1), default);
        Assert.Equal(second.Id, Assert.Single(page1.Items).Id);
        Assert.Equal(raw, page1.Items[0].RawResponseJson);
        Assert.Equal(second.CreatedAt, page1.Items[0].CreatedAt);
        Assert.True(page1.HasNextPage);
        var page2 = await handler.HandleAsync(new("btc", "main", 2, 1), default);
        Assert.Equal(first.Id, Assert.Single(page2.Items).Id);
        Assert.True(page2.HasNextPage);
        var page3 = await handler.HandleAsync(new("btc", "main", 3, 1), default);
        Assert.Equal(old.Id, Assert.Single(page3.Items).Id);
        Assert.False(page3.HasNextPage);
        Assert.Null(await repository.GetByIdAsync(testnet, first.Id, default));
        var testnetPage = await handler.HandleAsync(new("btc", "test3", 1, 1), default);
        Assert.Equal(testnet.Id, Assert.Single(testnetPage.Items).BlockchainNetworkId);
        Assert.False(testnetPage.HasNextPage);
        var emptyPage = await handler.HandleAsync(new("btc", "main", 4, 1), default);
        Assert.Empty(emptyPage.Items);
        Assert.False(emptyPage.HasNextPage);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
