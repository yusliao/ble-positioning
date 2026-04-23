using BlePositioning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlePositioning.Infrastructure.Persistence.Configurations;

public sealed class GeofenceEventConfiguration : IEntityTypeConfiguration<GeofenceEvent>
{
    public void Configure(EntityTypeBuilder<GeofenceEvent> builder)
    {
        builder.ToTable("geofence_events");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.DeviceId, x.OccurredAtUtc }).HasDatabaseName("ix_geofence_events_device_id_occurred_at_utc");
        builder.HasOne<TrackedDevice>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Floor>().WithMany().HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AlertRule>().WithMany().HasForeignKey(x => x.AlertRuleId).OnDelete(DeleteBehavior.Cascade);
    }
}
