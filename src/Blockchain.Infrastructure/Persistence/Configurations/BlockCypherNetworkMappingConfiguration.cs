using Blockchain.Domain;
using Blockchain.Infrastructure.BlockCypher;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blockchain.Infrastructure.Persistence.Configurations;

internal sealed class BlockCypherNetworkMappingConfiguration : IEntityTypeConfiguration<BlockCypherNetworkMapping>
{
    public void Configure(EntityTypeBuilder<BlockCypherNetworkMapping> builder)
    {
        builder.ToTable("BlockCypherNetworkMappings", table =>
        {
            table.HasCheckConstraint("CK_BlockCypherMappings_Currency", "\"CurrencyCode\" ~ '^[a-z0-9]{1,32}$'");
            table.HasCheckConstraint("CK_BlockCypherMappings_Network", "\"NetworkCode\" ~ '^[a-z0-9]{1,32}$'");
        });
        builder.HasKey(x => x.BlockchainNetworkId);
        builder.Property(x => x.CurrencyCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.NetworkCode).HasMaxLength(32).IsRequired();
        builder.Ignore(x => x.RelativePath);
        builder.Ignore(x => x.ExpectedResponseName);
        builder.HasIndex(x => new { x.CurrencyCode, x.NetworkCode }).IsUnique();
        builder.HasOne<BlockchainNetwork>()
            .WithMany()
            .HasForeignKey(x => x.BlockchainNetworkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
