using BlePositioning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlePositioning.Infrastructure.Persistence.Configurations;

public sealed class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> builder)
    {
        builder.ToTable("floors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BuildingCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.WidthMeters).HasColumnType("numeric(10,2)");
        builder.Property(x => x.HeightMeters).HasColumnType("numeric(10,2)");
        builder.Property(x => x.MapImageUrl).HasMaxLength(500);
        builder.HasMany(x => x.Beacons)
            .WithOne()
            .HasForeignKey(b => b.FloorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
