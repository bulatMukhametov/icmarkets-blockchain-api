using Blockchain.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blockchain.Infrastructure.Persistence.Configurations;

internal sealed class BlockchainNetworkConfiguration : IEntityTypeConfiguration<BlockchainNetwork>
{
    public void Configure(EntityTypeBuilder<BlockchainNetwork> builder)
    {
        builder.ToTable("BlockchainNetworks", table =>
        {
            table.HasCheckConstraint("CK_BlockchainNetworks_Code", "\"Code\" ~ '^[a-z0-9-]{1,32}$'");
            table.HasCheckConstraint("CK_BlockchainNetworks_Kind", "\"Kind\" IN (1, 2)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.CryptocurrencyId, x.Code }).IsUnique();
        builder.HasOne(x => x.Cryptocurrency)
            .WithMany()
            .HasForeignKey(x => x.CryptocurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
