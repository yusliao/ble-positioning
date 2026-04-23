using BlePositioning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlePositioning.Infrastructure.Persistence.Configurations;

public sealed class TrackedDeviceConfiguration : IEntityTypeConfiguration<TrackedDevice>
{
    public void Configure(EntityTypeBuilder<TrackedDevice> builder)
    {
        builder.ToTable("tracked_devices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeviceCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ApiKeyHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.DeviceCode).IsUnique();
        builder.HasIndex(x => x.ApiKeyHash)
            .IsUnique()
            .HasFilter("is_deleted = false");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
