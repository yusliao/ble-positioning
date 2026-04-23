using BlePositioning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlePositioning.Infrastructure.Persistence.Configurations;

public sealed class BeaconConfiguration : IEntityTypeConfiguration<Beacon>
{
    public void Configure(EntityTypeBuilder<Beacon> builder)
    {
        builder.ToTable("beacons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Uuid).HasMaxLength(36).IsRequired();
        builder.Property(x => x.X).HasColumnType("numeric(10,2)");
        builder.Property(x => x.Y).HasColumnType("numeric(10,2)");
        builder.HasIndex(x => new { x.Uuid, x.Major, x.Minor })
            .IsUnique()
            .HasFilter("is_deleted = false");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
