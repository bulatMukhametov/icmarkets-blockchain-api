using System.Collections.Concurrent;
using System.Net;
using Blockchain.Application.Abstractions;
using Blockchain.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blockchain.FunctionalTests;

public sealed class ApiFactory(string? databaseConnection = null) : WebApplicationFactory<Program>
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
                ["BlockCypher:MaxRetries"] = "0"
            }));
        builder.ConfigureTestServices(services =>
        {
            if (databaseConnection is null)
            {
                services.RemoveAll<IBlockchainSnapshotRepository>();
                services.AddSingleton<IBlockchainSnapshotRepository>(Store);
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
    private readonly ConcurrentDictionary<long, BlockchainSnapshot> snapshots = new();

    public Task AddAsync(BlockchainSnapshot snapshot, CancellationToken token)
    {
        var id = Interlocked.Increment(ref nextId);
        typeof(BlockchainSnapshot).GetProperty(nameof(BlockchainSnapshot.Id))!.SetValue(snapshot, id);
        snapshots[id] = snapshot;
        return Task.CompletedTask;
    }

    public Task<BlockchainSnapshot?> GetByIdAsync(ChainId chain, long id, CancellationToken token) =>
        Task.FromResult(snapshots.TryGetValue(id, out var value) && value.Coin == chain.Coin && value.Network == chain.Network
            ? value : null);

    public Task<IReadOnlyList<BlockchainSnapshot>> GetHistoryAsync(ChainId chain, SnapshotCursor? before, int take,
        CancellationToken token) => Task.FromResult<IReadOnlyList<BlockchainSnapshot>>(snapshots.Values
        .Where(x => x.Coin == chain.Coin && x.Network == chain.Network)
        .Where(x => before is null || x.CreatedAt < before.CreatedAt || (x.CreatedAt == before.CreatedAt && x.Id < before.Id))
        .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(take).ToArray());
}
