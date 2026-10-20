using Blockchain.Application.Networks;
using Blockchain.Application.Providers;
using Blockchain.Application.Snapshots;
using Blockchain.Infrastructure.BlockCypher;
using Blockchain.Infrastructure.Collection;
using Blockchain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Blockchain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        AddPersistence(services);
        AddBlockCypher(services, configuration);
        AddSnapshotCollection(services, configuration);
        return services;
    }

    private static void AddPersistence(IServiceCollection services)
    {
        services.AddDbContext<BlockchainDbContext>((provider, options) =>
        {
            var connection = provider.GetRequiredService<IConfiguration>().GetConnectionString("Blockchain")
                ?? throw new InvalidOperationException("ConnectionStrings:Blockchain is required.");
            options.UseNpgsql(connection);
        });
        services.AddScoped<IBlockchainSnapshotRepository, BlockchainSnapshotRepository>();
        services.AddScoped<IBlockchainNetworkCatalog, BlockchainNetworkCatalog>();
        services.AddScoped<IBlockCypherNetworkMappings, BlockCypherNetworkMappings>();
    }

    private static void AddSnapshotCollection(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISnapshotCaptureService, ScopedSnapshotCaptureService>();
        services.AddOptions<SnapshotCollectionOptions>()
            .Bind(configuration.GetSection(SnapshotCollectionOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHostedService<SnapshotCollectionWorker>();
    }

    private static void AddBlockCypher(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BlockCypherOptions>()
            .Bind(configuration.GetSection(BlockCypherOptions.Section))
            .ValidateDataAnnotations()
            .Validate(settings => IsSupportedBaseUrl(settings.BaseUrl),
                "BaseUrl must be HTTPS (or loopback for tests), with a trailing slash.")
            .ValidateOnStart();
        services.AddSingleton<ProviderRequestLimiter>();
        services.AddSingleton<ProviderCircuitBreaker>();
        services.AddHttpClient<IBlockchainDataClient, BlockCypherClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<BlockCypherOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });
    }

    private static bool IsSupportedBaseUrl(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback) &&
        uri.AbsolutePath.EndsWith('/');
}
