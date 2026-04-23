namespace BlePositioning.Infrastructure.Geofence;

public sealed class RedisGeofenceStateOptions
{
    public const string SectionName = "GeofenceState";

    public TimeSpan Ttl { get; set; } = TimeSpan.FromDays(7);
}
