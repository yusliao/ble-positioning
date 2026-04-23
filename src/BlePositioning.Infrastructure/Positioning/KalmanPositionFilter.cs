using BlePositioning.Application.Positioning;
using BlePositioning.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlePositioning.Infrastructure.Positioning;

public sealed class KalmanPositionFilter(
    IOptions<PositioningOptions> options,
    IKalmanStateStore store,
    ILogger<KalmanPositionFilter> logger) : IKalmanPositionFilter
{
    public async Task<(double X, double Y, double Accuracy)> SmoothAsync(
        Guid deviceId,
        double measX,
        double measY,
        DateTime measuredAtUtc,
        CancellationToken ct = default)
    {
        var o = options.Value;
        KalmanFilterState? prior = null;
        try
        {
            prior = await store.GetAsync(deviceId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kalman state get failed for {DeviceId}, re-initializing", deviceId);
        }

        var (next, outX, outY, outAcc) = KalmanFilter2DMath.Update(
            prior,
            measX,
            measY,
            measuredAtUtc,
            o.KalmanProcessNoise,
            o.KalmanMeasurementNoise);
        try
        {
            var ttl = TimeSpan.FromSeconds(Math.Max(1, o.KalmanStateTtlSeconds));
            await store.SetAsync(deviceId, next, ttl, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kalman state set failed for {DeviceId}", deviceId);
        }

        return (outX, outY, outAcc);
    }
}
