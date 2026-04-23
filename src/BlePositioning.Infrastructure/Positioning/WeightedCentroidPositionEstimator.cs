using BlePositioning.Application.Positioning;

namespace BlePositioning.Infrastructure.Positioning;

public static class WeightedCentroidPositionEstimator
{
    public static (double X, double Y, double Accuracy)? Estimate(
        IReadOnlyList<(BeaconPlacement Beacon, double Distance)> beacons)
    {
        if (beacons.Count < 3)
            return null;

        var take = beacons.OrderBy(b => b.Distance).Take(5).ToList();
        double sumW = 0, wx = 0, wy = 0;
        foreach (var (beacon, dist) in take)
        {
            var w = 1.0 / Math.Max(dist * dist, 0.01);
            sumW += w;
            wx += w * beacon.X;
            wy += w * beacon.Y;
        }

        if (sumW <= 0)
            return null;

        var x = wx / sumW;
        var y = wy / sumW;
        var rmse = Math.Sqrt(take.Average(t =>
        {
            var dx = t.Beacon.X - x;
            var dy = t.Beacon.Y - y;
            return dx * dx + dy * dy;
        }));
        return (x, y, Math.Max(0.5, rmse));
    }

    public static (double X, double Y) ClampToFloor(double x, double y, BeaconPlacement reference)
    {
        var x2 = Math.Clamp(x, 0, reference.FloorWidthMeters);
        var y2 = Math.Clamp(y, 0, reference.FloorHeightMeters);
        return (x2, y2);
    }
}
