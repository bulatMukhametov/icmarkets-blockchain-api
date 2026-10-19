using System.Net;
using System.Net.Http.Json;
using Blockchain.Api.Contracts;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Tests.Shared;

namespace Blockchain.FunctionalTests;

public sealed class DatabaseApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task Capture_and_history_roundtrip_through_http_and_postgresql()
    {
        await postgres.StartAsync();
        await using var factory = new ApiFactory(postgres.ConnectionString);
        using var http = factory.CreateClient();
        using var first = await http.PostAsync("/api/chains/btc/main/snapshots", null);
        using var second = await http.PostAsync("/api/chains/btc/main/snapshots", null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var newest = (await second.Content.ReadFromJsonAsync<SnapshotResponse>())!;
        var page1 = (await http.GetFromJsonAsync<SnapshotHistoryResponse>("/api/chains/btc/main/snapshots?pageSize=1"))!;
        Assert.Equal(newest.Id, Assert.Single(page1.Items).Id);
        var page2 = (await http.GetFromJsonAsync<SnapshotHistoryResponse>(
            "/api/chains/btc/main/snapshots?pageSize=1&cursor=" + Uri.EscapeDataString(page1.NextCursor!)))!;
        Assert.True(Assert.Single(page2.Items).Id < newest.Id);
        Assert.Null(page2.NextCursor);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(2, factory.ProviderRequests);

        factory.FailingCoin = "eth";
        using var collection = await http.PostAsync("/api/snapshot-collections", null);
        Assert.Equal(HttpStatusCode.OK, collection.StatusCode);
        var results = (await collection.Content.ReadFromJsonAsync<NetworkCaptureResult[]>())!;
        Assert.Equal(4, results.Count(x => x.Succeeded));
        foreach (var item in results.Where(x => x.Succeeded))
        {
            using var persisted = await http.GetAsync($"/api/chains/{item.Coin}/{item.Network}/snapshots/{item.SnapshotId}");
            Assert.Equal(HttpStatusCode.OK, persisted.StatusCode);
        }
    }
}
