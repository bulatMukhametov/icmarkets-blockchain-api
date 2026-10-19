using System.Net;
using Blockchain.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.FunctionalTests;

public sealed class CatalogReadinessTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [DatabaseFact]
    public async Task Readiness_requires_the_catalog_migration_while_liveness_does_not()
    {
        await postgres.StartAsync("20261018192339_InitialSnapshots");
        await using var factory = new ApiFactory(postgres.ConnectionString);
        using var http = factory.CreateClient();
        using var live = await http.GetAsync("/health/live");
        using var notReady = await http.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, notReady.StatusCode);
        await using (var db = postgres.CreateContext()) await db.Database.MigrateAsync();
        using var ready = await http.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(0, factory.ProviderRequests);
    }
}
