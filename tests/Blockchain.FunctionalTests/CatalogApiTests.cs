using System.Net;
using System.Net.Http.Json;
using Blockchain.Api.Contracts;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Domain;
using Blockchain.Infrastructure.BlockCypher;
using Blockchain.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.FunctionalTests;

public sealed class CatalogApiTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [DatabaseFact]
    public async Task New_currency_and_provider_mapping_work_without_changing_handlers_or_schema()
    {
        await postgres.StartAsync();
        await using (var db = postgres.CreateContext())
        {
            var currency = new Cryptocurrency(Guid.NewGuid(), "doge", "Dogecoin");
            var network = new BlockchainNetwork(Guid.NewGuid(), currency, "production", NetworkKind.Mainnet);
            db.Networks.Add(network);
            // Public network code is deliberately different from the provider's URL and JSON name.
            db.BlockCypherMappings.Add(new BlockCypherNetworkMapping(network.Id, "doge", "main"));
            await db.SaveChangesAsync();
        }

        await using var factory = new ApiFactory(postgres.ConnectionString);
        using var http = factory.CreateClient();
        var networks = (await http.GetFromJsonAsync<BlockchainNetworkResponse[]>("/api/chains"))!;
        Assert.Equal(6, networks.Length);
        Assert.Contains(networks, x => x.Coin == "doge" && x.Network == "production");
        using var capture = await http.PostAsync("/api/chains/DOGE/PRODUCTION/snapshots", null);
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);
        var snapshot = (await capture.Content.ReadFromJsonAsync<SnapshotResponse>())!;
        Assert.Equal("doge", snapshot.Coin);
        Assert.Equal("production", snapshot.Network);
        Assert.Equal("DOGE.main", snapshot.Payload.GetProperty("name").GetString());
        using var saved = await http.GetAsync(capture.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var batch = await http.PostAsync("/api/snapshot-collections", null);
        var results = (await batch.Content.ReadFromJsonAsync<NetworkCaptureResponse[]>())!;
        Assert.Equal(6, results.Length);
        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(7, factory.ProviderRequests);
        var history = (await http.GetFromJsonAsync<SnapshotHistoryResponse>("/api/chains/doge/production/snapshots"))!;
        Assert.Equal(2, history.Items.Count);
        Assert.Equal(7, factory.ProviderRequests);

        await using (var db = postgres.CreateContext())
        {
            db.BlockCypherMappings.Remove(await db.BlockCypherMappings.SingleAsync(x => x.CurrencyCode == "doge"));
            await db.SaveChangesAsync();
        }
        using var unmapped = await http.PostAsync("/api/chains/doge/production/snapshots", null);
        Assert.Equal(HttpStatusCode.BadGateway, unmapped.StatusCode);
        Assert.Equal(7, factory.ProviderRequests);
        var preserved = (await http.GetFromJsonAsync<SnapshotHistoryResponse>("/api/chains/doge/production/snapshots"))!;
        Assert.Equal(2, preserved.Items.Count);
    }
}
