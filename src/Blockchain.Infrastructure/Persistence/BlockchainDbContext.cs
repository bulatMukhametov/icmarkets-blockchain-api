using Blockchain.Domain;
using Blockchain.Infrastructure.BlockCypher;
using Blockchain.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.Infrastructure.Persistence;

public sealed class BlockchainDbContext(DbContextOptions<BlockchainDbContext> options) : DbContext(options)
{
    public DbSet<BlockchainNetworkSnapshot> Snapshots => Set<BlockchainNetworkSnapshot>();
    public DbSet<Cryptocurrency> Cryptocurrencies => Set<Cryptocurrency>();
    public DbSet<BlockchainNetwork> Networks => Set<BlockchainNetwork>();
    public DbSet<BlockCypherNetworkMapping> BlockCypherMappings => Set<BlockCypherNetworkMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CryptocurrencyConfiguration());
        modelBuilder.ApplyConfiguration(new BlockchainNetworkConfiguration());
        modelBuilder.ApplyConfiguration(new BlockCypherNetworkMappingConfiguration());
        modelBuilder.ApplyConfiguration(new BlockchainNetworkSnapshotConfiguration());
        InitialNetworkCatalog.Configure(modelBuilder);
    }
}
