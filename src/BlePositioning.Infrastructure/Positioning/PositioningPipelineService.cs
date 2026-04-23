using System.Collections.Concurrent;
using BlePositioning.Application.Common.Interfaces;
using BlePositioning.Application.Devices;
using BlePositioning.Application.Geofence;
using BlePositioning.Application.Positioning;
using BlePositioning.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlePositioning.Infrastructure.Positioning;

public sealed class PositioningPipelineService(
    RssiIngestChannel ingest,
    IBeaconLookup beaconLookup,
    IPositionCache positionCache,
    ITrajectoryBulkWriter trajectoryWriter,
    IPositioningNotificationService positionNotifications,
    IDevicePresenceCoordinator devicePresence,
    IKalmanPositionFilter kalman,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<PositioningOptions> options,
    ILogger<PositioningPipelineService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, (double X, double Y)> _smooth = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opt = options.Value;
        await foreach (var report in ingest.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(report, opt, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Positioning failed for device {DeviceId}", report.DeviceId);
            }
        }
    }

    private async Task ProcessAsync(RssiReportDto report, PositioningOptions opt, CancellationToken ct)
    {
        var keys = report.Signals.Select(s => (s.Uuid, s.Major, s.Minor)).ToList();
        var beacons = await beaconLookup.ResolveAsync(keys, ct);
        if (beacons.Count < opt.MinBeaconsRequired)
        {
            logger.LogWarning("Insufficient beacons for device {DeviceId}", report.DeviceId);
            return;
        }

        var placements = new List<(BeaconPlacement Beacon, double Distance)>();
        foreach (var signal in report.Signals)
        {
            var b = beacons.FirstOrDefault(x =>
                x.Uuid.Equals(signal.Uuid, StringComparison.OrdinalIgnoreCase)
                && x.Major == signal.Major
                && x.Minor == signal.Minor);
            if (b is null)
                continue;
            var dist = PathLossCalculator.RssiToDistance(signal.Rssi, b.TxPower, opt.PathLossExponent);
            placements.Add((b, dist));
        }

        if (placements.Count < opt.MinBeaconsRequired)
            return;

        var estimate = WeightedCentroidPositionEstimator.Estimate(placements);
        if (estimate is null)
            return;

        var reference = placements.OrderBy(p => p.Distance).First().Beacon;
        var (cx, cy) = WeightedCentroidPositionEstimator.ClampToFloor(estimate.Value.X, estimate.Value.Y, reference);

        var ts = report.Timestamp.Kind == DateTimeKind.Utc ? report.Timestamp : report.Timestamp.ToUniversalTime();
        double sx, sy, acc;
        if (opt.UseKalmanFilter)
        {
            (sx, sy, var kAcc) = await kalman.SmoothAsync(report.DeviceId, cx, cy, ts, ct).ConfigureAwait(false);
            acc = Math.Max(estimate.Value.Accuracy, kAcc);
            if (acc < 0.5)
                acc = 0.5;
        }
        else
        {
            var prev = _smooth.GetOrAdd(report.DeviceId, _ => (cx, cy));
            const double alpha = 0.35;
            sx = alpha * cx + (1 - alpha) * prev.X;
            sy = alpha * cy + (1 - alpha) * prev.Y;
            _smooth[report.DeviceId] = (sx, sy);
            acc = estimate.Value.Accuracy;
        }

        var cached = new CachedPosition(reference.FloorId, sx, sy, acc, ts);
        await positionCache.SetAsync(report.DeviceId, cached, ct);
        try
        {
            await devicePresence.OnPositionCacheUpdatedAsync(report.DeviceId, ts, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Device presence after position failed for {DeviceId}", report.DeviceId);
        }

        var row = new PositionLogRow(report.DeviceId, reference.FloorId, sx, sy, acc, ts);
        await trajectoryWriter.WriteBatchAsync([row], ct);

        try
        {
            await positionNotifications.NotifyPositionUpdatedAsync(
                reference.FloorId, report.DeviceId, sx, sy, acc, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR position notify failed for device {DeviceId}", report.DeviceId);
        }

        try
        {
            await using (var evalScope = serviceScopeFactory.CreateAsyncScope())
            {
                var geofence = evalScope.ServiceProvider.GetRequiredService<IGeofenceEvaluationService>();
                await geofence.EvaluateAsync(
                    report.DeviceId,
                    reference.FloorId,
                    sx,
                    sy,
                    acc,
                    ts,
                    ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Geofence evaluation failed for device {DeviceId}", report.DeviceId);
        }
    }
}
