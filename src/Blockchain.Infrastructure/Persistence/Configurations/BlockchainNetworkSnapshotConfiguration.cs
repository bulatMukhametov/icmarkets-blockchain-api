using Blockchain.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blockchain.Infrastructure.Persistence.Configurations;

internal sealed class BlockchainNetworkSnapshotConfiguration : IEntityTypeConfiguration<BlockchainNetworkSnapshot>
{
    public void Configure(EntityTypeBuilder<BlockchainNetworkSnapshot> builder)
    {
        builder.ToTable("Snapshots", table =>
        {
            table.HasCheckConstraint("CK_Snapshots_Payload", "json_typeof(\"Payload\"::json) = 'object'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityByDefaultColumn();
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamp with time zone");
        builder.Property(x => x.RawResponseJson).HasColumnName("Payload").HasColumnType("text").IsRequired();
        builder.HasOne(x => x.BlockchainNetwork)
            .WithMany()
            .HasForeignKey(x => x.BlockchainNetworkId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BlockchainNetworkId, x.CreatedAt, x.Id }).IsDescending(false, true, true);
    }
}
