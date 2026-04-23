using BlePositioning.Domain;
using Microsoft.EntityFrameworkCore;

namespace BlePositioning.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Beacon> Beacons => Set<Beacon>();
    public DbSet<TrackedDevice> TrackedDevices => Set<TrackedDevice>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<GeofenceEvent> GeofenceEvents => Set<GeofenceEvent>();
    public DbSet<DevicePresenceEvent> DevicePresenceEvents => Set<DevicePresenceEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
