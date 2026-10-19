using Blockchain.Application.Abstractions;
using Blockchain.Infrastructure.BlockCypher;
using Blockchain.Infrastructure.Collection;
using Blockchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blockchain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BlockchainDbContext>((provider, options) =>
        {
            var connection = provider.GetRequiredService<IConfiguration>().GetConnectionString("Blockchain")
                ?? throw new InvalidOperationException("ConnectionStrings:Blockchain is required.");
            options.UseNpgsql(connection);
        });
        services.AddScoped<IBlockchainSnapshotRepository, BlockchainSnapshotRepository>();
        services.AddSingleton<ISnapshotCapture, ScopedSnapshotCapture>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<SnapshotCollectionOptions>().Bind(configuration.GetSection(SnapshotCollectionOptions.Section))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddHostedService<SnapshotCollectionWorker>();
        services.AddOptions<BlockCypherOptions>().Bind(configuration.GetSection(BlockCypherOptions.Section))
            .ValidateDataAnnotations()
            .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback) && uri.AbsolutePath.EndsWith('/'),
                "BaseUrl must be HTTPS (or loopback for tests), with a trailing slash.")
            .ValidateOnStart();
        services.AddSingleton<ProviderRequestGate>();
        services.AddSingleton<ProviderCircuitBreaker>();
        services.AddHttpClient<IBlockchainDataClient, BlockCypherClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlockCypherOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });
        return services;
    }
}
