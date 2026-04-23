using BlePositioning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlePositioning.Infrastructure.Persistence.Configurations;

public sealed class DevicePresenceEventConfiguration : IEntityTypeConfiguration<DevicePresenceEvent>
{
    public void Configure(EntityTypeBuilder<DevicePresenceEvent> builder)
    {
        builder.ToTable("device_presence_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventKind).HasColumnName("event_kind");
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.HasIndex(x => new { x.DeviceId, x.OccurredAtUtc });
    }
}
