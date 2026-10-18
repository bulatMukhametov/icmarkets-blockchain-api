using Blockchain.Domain;
using Microsoft.EntityFrameworkCore;

namespace Blockchain.Infrastructure.Persistence;

public sealed class BlockchainDbContext(DbContextOptions<BlockchainDbContext> options) : DbContext(options)
{
    public DbSet<BlockchainSnapshot> Snapshots => Set<BlockchainSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BlockchainSnapshot>();
        entity.ToTable("Snapshots", table =>
        {
            table.HasCheckConstraint("CK_Snapshots_Chain", "(\"Coin\" IN ('btc','eth','dash','ltc') AND \"Network\" = 'main') OR (\"Coin\" = 'btc' AND \"Network\" = 'test3')");
            table.HasCheckConstraint("CK_Snapshots_Payload", "json_typeof(\"Payload\"::json) = 'object'");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).UseIdentityByDefaultColumn();
        entity.Property(x => x.Coin).HasMaxLength(8).IsRequired();
        entity.Property(x => x.Network).HasMaxLength(8).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        entity.Property(x => x.Payload).HasColumnType("text").IsRequired();
        entity.HasIndex(x => new { x.Coin, x.Network, x.CreatedAt, x.Id })
            .IsDescending(false, false, true, true);
    }
}
