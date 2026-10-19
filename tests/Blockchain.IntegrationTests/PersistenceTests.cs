using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.GetSnapshotHistory;
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
        var main = BlockchainNetwork.FindSupportedNetwork("btc", "main")!;
        var testnet = BlockchainNetwork.FindSupportedNetwork("btc", "test3")!;
        var time = DateTimeOffset.UtcNow.AddTicks(17);
        const string raw = "{ \"name\": \"BTC.main\", \"large\": 12345678901234567890, \"extra\": [true,null] }";
        var old = new BlockchainNetworkSnapshot(main, time.AddMinutes(-1), raw);
        var first = new BlockchainNetworkSnapshot(main, time, raw);
        var second = new BlockchainNetworkSnapshot(main, time, raw);
        await repository.AddAndSaveAsync(old, default);
        await repository.AddAndSaveAsync(first, default);
        await repository.AddAndSaveAsync(second, default);
        await repository.AddAndSaveAsync(new(testnet, time.AddHours(1), "{}"), default);
        db.ChangeTracker.Clear();

        var handler = new GetSnapshotHistoryHandler(repository);
        var page1 = await handler.HandleAsync(new(main, 1), default);
        Assert.Equal(second.Id, Assert.Single(page1.Items).Id);
        Assert.Equal(raw, page1.Items[0].Payload);
        Assert.Equal(second.CreatedAt, page1.Items[0].CreatedAt);
        Assert.NotNull(page1.NextCursor);
        var page2 = await handler.HandleAsync(new(main, 1, page1.NextCursor), default);
        Assert.Equal(first.Id, Assert.Single(page2.Items).Id);
        var page3 = await handler.HandleAsync(new(main, 1, page2.NextCursor), default);
        Assert.Equal(old.Id, Assert.Single(page3.Items).Id);
        Assert.Null(page3.NextCursor);
        Assert.Null(await repository.GetByIdAsync(testnet, first.Id, default));
        await Assert.ThrowsAsync<RequestValidationException>(() => handler.HandleAsync(new(testnet, 1, page1.NextCursor), default));
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
