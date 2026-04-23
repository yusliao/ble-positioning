using BlePositioning.Application.Floors;
using BlePositioning.Application.Geofence;
using BlePositioning.Domain;
using BlePositioning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlePositioning.Infrastructure.Geofence;

public sealed class GeofenceEvaluationService(
    IFloorRepository floorRepository,
    AppDbContext db,
    IGeofenceStateStore stateStore,
    IGeofenceEventPublisher eventPublisher,
    ILogger<GeofenceEvaluationService> logger) : IGeofenceEvaluationService
{
    public async Task EvaluateAsync(
        Guid deviceId,
        Guid floorId,
        double x,
        double y,
        double accuracy,
        DateTime occurredAtUtc,
        CancellationToken ct = default)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            occurredAtUtc = occurredAtUtc.ToUniversalTime();

        var rules = await floorRepository.ListAlertRulesByFloorIdAsync(floorId, ct);
        var enabled = rules.Where(r => r.IsEnabled).ToList();
        if (enabled.Count == 0)
            return;

        var toSave = new List<GeofenceEvent>();
        var notifications = new List<GeofenceEventNotification>();
        var newInsideByRule = new List<(Guid RuleId, bool Inside)>(enabled.Count);

        foreach (var rule in enabled)
        {
            if (!ZonePolygonRingParser.TryGetExteriorRing(rule.ZonePolygon, out var ring))
            {
                logger.LogWarning("Skipping alert rule {RuleId}: cannot parse zone polygon.", rule.Id);
                continue;
            }

            var inside = PointInPolygon.RayCastingContains(ring, x, y);
            var wasInside = await stateStore.GetWasInsideAsync(deviceId, rule.Id, ct);
            var trigger = (AlertTriggerKind)rule.TriggerOn;

            var enterEdge = !wasInside && inside;
            var exitEdge = wasInside && !inside;

            var wantEnter = enterEdge && (trigger is AlertTriggerKind.Enter or AlertTriggerKind.EnterOrExit);
            var wantExit = exitEdge && (trigger is AlertTriggerKind.Exit or AlertTriggerKind.EnterOrExit);

            if (wantEnter)
            {
                toSave.Add(GeofenceEvent.Create(
                    deviceId, floorId, rule.Id, GeofenceEvent.KindEnter, x, y, accuracy, occurredAtUtc));
                notifications.Add(new GeofenceEventNotification(
                    deviceId, floorId, rule.Id, GeofenceEvent.KindEnter, x, y, occurredAtUtc));
            }

            if (wantExit)
            {
                toSave.Add(GeofenceEvent.Create(
                    deviceId, floorId, rule.Id, GeofenceEvent.KindExit, x, y, accuracy, occurredAtUtc));
                notifications.Add(new GeofenceEventNotification(
                    deviceId, floorId, rule.Id, GeofenceEvent.KindExit, x, y, occurredAtUtc));
            }

            newInsideByRule.Add((rule.Id, inside));
        }

        if (toSave.Count > 0)
        {
            db.GeofenceEvents.AddRange(toSave);
            await db.SaveChangesAsync(ct);
        }

        foreach (var p in newInsideByRule)
            await stateStore.SetWasInsideAsync(deviceId, p.RuleId, p.Inside, ct);

        foreach (var n in notifications)
        {
            try
            {
                await eventPublisher.PublishAsync(n, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Geofence event publish failed for device {DeviceId}", deviceId);
            }
        }
    }
}
