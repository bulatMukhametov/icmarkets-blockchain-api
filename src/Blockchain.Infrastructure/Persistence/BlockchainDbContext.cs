using Blockchain.Domain;
using Blockchain.Infrastructure.BlockCypher;
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
        var currency = modelBuilder.Entity<Cryptocurrency>();
        currency.ToTable("Cryptocurrencies", table =>
            table.HasCheckConstraint("CK_Cryptocurrencies_Code", "\"Code\" ~ '^[a-z0-9-]{1,32}$'"));
        currency.HasKey(x => x.Id);
        currency.Property(x => x.Id).ValueGeneratedNever();
        currency.Property(x => x.Code).HasMaxLength(32).IsRequired();
        currency.Property(x => x.Name).HasMaxLength(100).IsRequired();
        currency.HasIndex(x => x.Code).IsUnique();

        var network = modelBuilder.Entity<BlockchainNetwork>();
        network.ToTable("BlockchainNetworks", table =>
        {
            table.HasCheckConstraint("CK_BlockchainNetworks_Code", "\"Code\" ~ '^[a-z0-9-]{1,32}$'");
            table.HasCheckConstraint("CK_BlockchainNetworks_Kind", "\"Kind\" IN (1, 2)");
        });
        network.HasKey(x => x.Id);
        network.Property(x => x.Id).ValueGeneratedNever();
        network.Property(x => x.Code).HasMaxLength(32).IsRequired();
        network.HasIndex(x => new { x.CryptocurrencyId, x.Code }).IsUnique();
        network.HasOne(x => x.Cryptocurrency).WithMany().HasForeignKey(x => x.CryptocurrencyId).OnDelete(DeleteBehavior.Restrict);

        var mapping = modelBuilder.Entity<BlockCypherNetworkMapping>();
        mapping.ToTable("BlockCypherNetworkMappings", table =>
        {
            table.HasCheckConstraint("CK_BlockCypherMappings_Currency", "\"CurrencyCode\" ~ '^[a-z0-9]{1,32}$'");
            table.HasCheckConstraint("CK_BlockCypherMappings_Network", "\"NetworkCode\" ~ '^[a-z0-9]{1,32}$'");
        });
        mapping.HasKey(x => x.BlockchainNetworkId);
        mapping.Property(x => x.CurrencyCode).HasMaxLength(32).IsRequired();
        mapping.Property(x => x.NetworkCode).HasMaxLength(32).IsRequired();
        mapping.Ignore(x => x.RelativePath);
        mapping.Ignore(x => x.ExpectedResponseName);
        mapping.HasIndex(x => new { x.CurrencyCode, x.NetworkCode }).IsUnique();
        mapping.HasOne<BlockchainNetwork>().WithMany().HasForeignKey(x => x.BlockchainNetworkId).OnDelete(DeleteBehavior.Restrict);

        var entity = modelBuilder.Entity<BlockchainNetworkSnapshot>();
        entity.ToTable("Snapshots", table =>
        {
            table.HasCheckConstraint("CK_Snapshots_Payload", "json_typeof(\"Payload\"::json) = 'object'");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).UseIdentityByDefaultColumn();
        entity.Property(x => x.RequestedAtUtc).HasColumnName("CreatedAt").HasColumnType("timestamp with time zone");
        entity.Property(x => x.RawResponseJson).HasColumnName("Payload").HasColumnType("text").IsRequired();
        entity.HasOne(x => x.BlockchainNetwork).WithMany().HasForeignKey(x => x.BlockchainNetworkId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.BlockchainNetworkId, x.RequestedAtUtc, x.Id }).IsDescending(false, true, true);
        InitialNetworkCatalog.Configure(modelBuilder);
    }
}
