using System.Collections.Concurrent;
using System.Net;
using Blockchain.Application.Networks;
using Blockchain.Application.Providers;
using Blockchain.Application.Snapshots;
using Blockchain.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blockchain.FunctionalTests;

public sealed class ApiFactory(string? databaseConnection = null, bool collectionEnabled = false) : WebApplicationFactory<Program>
{
    public int ProviderRequests;
    public string? FailingCoin { get; set; }
    public MemorySnapshotRepository Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Blockchain"] = databaseConnection ?? "Host=localhost;Port=1;Database=absent;Username=test;Password=test;Timeout=1",
                ["BlockCypher:RequestsPerSecond"] = "3",
                ["BlockCypher:MaxRetries"] = "0",
                ["SnapshotCollection:Enabled"] = collectionEnabled.ToString(),
                ["SnapshotCollection:InitialDelaySeconds"] = "0"
            }));
        builder.ConfigureTestServices(services =>
        {
            if (databaseConnection is null)
            {
                services.RemoveAll<IBlockchainSnapshotRepository>();
                services.AddSingleton<IBlockchainSnapshotRepository>(Store);
                services.RemoveAll<IBlockchainNetworkCatalog>();
                services.AddSingleton<IBlockchainNetworkCatalog>(new Blockchain.Tests.Shared.TestNetworkCatalog());
                services.RemoveAll<Blockchain.Infrastructure.BlockCypher.IBlockCypherNetworkMappings>();
                services.AddSingleton<Blockchain.Infrastructure.BlockCypher.IBlockCypherNetworkMappings>(new Blockchain.Tests.Shared.TestBlockCypherMappings());
            }
            services.AddHttpClient<IBlockchainDataClient, Blockchain.Infrastructure.BlockCypher.BlockCypherClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new ProviderHandler(this));
        });
    }

    private sealed class ProviderHandler(ApiFactory factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref factory.ProviderRequests);
            var segments = request.RequestUri!.AbsolutePath.Trim('/').Split('/');
            var coin = segments[^2];
            var network = segments[^1];
            return Task.FromResult(coin == factory.FailingCoin ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{ "name": "{{coin.ToUpperInvariant()}}.{{network}}", "height": 42, "unknown": {"large":12345678901234567890} }""")
                });
        }
    }
}

// Test double for HTTP contract tests only. Database tests use the actual EF repository.
public sealed class MemorySnapshotRepository : IBlockchainSnapshotRepository
{
    private long nextId;
    private readonly ConcurrentDictionary<long, BlockchainNetworkSnapshot> snapshots = new();

    public Task SaveAsync(BlockchainNetworkSnapshot snapshot, CancellationToken token)
    {
        var id = Interlocked.Increment(ref nextId);
        typeof(BlockchainNetworkSnapshot).GetProperty(nameof(BlockchainNetworkSnapshot.Id))!.SetValue(snapshot, id);
        snapshots[id] = snapshot;
        return Task.CompletedTask;
    }

    public Task<BlockchainNetworkSnapshot?> GetByIdAsync(BlockchainNetwork network, long id, CancellationToken token) =>
        Task.FromResult(snapshots.TryGetValue(id, out var value) && value.BlockchainNetworkId == network.Id
            ? value : null);

    public Task<IReadOnlyList<BlockchainNetworkSnapshot>> GetHistoryAsync(BlockchainNetwork network, int skip, int take,
        CancellationToken token) => Task.FromResult<IReadOnlyList<BlockchainNetworkSnapshot>>(snapshots.Values
        .Where(x => x.BlockchainNetworkId == network.Id)
        .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(skip).Take(take).ToArray());
}
