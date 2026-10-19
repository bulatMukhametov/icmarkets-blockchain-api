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
        Assert.Equal(1, page1.Page);
        Assert.Equal(1, page1.PageSize);
        Assert.True(page1.HasNextPage);
        var page2 = (await http.GetFromJsonAsync<SnapshotHistoryResponse>(
            "/api/chains/btc/main/snapshots?page=2&pageSize=1"))!;
        Assert.True(Assert.Single(page2.Items).Id < newest.Id);
        Assert.Equal(2, page2.Page);
        Assert.False(page2.HasNextPage);
        var emptyPage = (await http.GetFromJsonAsync<SnapshotHistoryResponse>(
            "/api/chains/btc/main/snapshots?page=3&pageSize=1"))!;
        Assert.Empty(emptyPage.Items);
        Assert.False(emptyPage.HasNextPage);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(2, factory.ProviderRequests);

        factory.FailingCoin = "eth";
        using var collection = await http.PostAsync("/api/snapshot-collections", null);
        Assert.Equal(HttpStatusCode.OK, collection.StatusCode);
        var results = (await collection.Content.ReadFromJsonAsync<NetworkCaptureResponse[]>())!;
        Assert.Equal(4, results.Count(x => x.Succeeded));
        foreach (var item in results.Where(x => x.Succeeded))
        {
            using var persisted = await http.GetAsync($"/api/chains/{item.Coin}/{item.Network}/snapshots/{item.SnapshotId}");
            Assert.Equal(HttpStatusCode.OK, persisted.StatusCode);
        }
    }
}
