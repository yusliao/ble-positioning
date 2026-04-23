using BlePositioning.Application.Positioning;
using Xunit;

namespace BlePositioning.Tests;

public sealed class KalmanFilter2DMathTests
{
    private const double Q = 0.05;
    private const double R = 4.0;
    private static DateTime T(int seconds) => new(2026, 4, 23, 0, 0, seconds, DateTimeKind.Utc);

    [Fact]
    public void First_measurement_initializes_to_z()
    {
        var t = T(0);
        var (s, x, y, acc) = KalmanFilter2DMath.Update(null, 3.0, 4.0, t, Q, R);
        Assert.Equal(3.0, x, precision: 9);
        Assert.Equal(4.0, y, precision: 9);
        Assert.Equal(3.0, s.X);
        Assert.Equal(4.0, s.Y);
        Assert.True(acc > 0);
    }

    [Fact]
    public void Repeating_same_z_converges_to_measurement_is_deterministic()
    {
        var t0 = T(0);
        var t1 = T(1);
        var t2 = T(2);
        var (s0, x0, y0, _) = KalmanFilter2DMath.Update(null, 10.0, 20.0, t0, Q, R);
        var (s1, x1, y1, _) = KalmanFilter2DMath.Update(s0, 10.0, 20.0, t1, Q, R);
        var (s2, x2, y2, _) = KalmanFilter2DMath.Update(s1, 10.0, 20.0, t2, Q, R);
        Assert.Equal(x1, x2, precision: 9);
        Assert.Equal(y1, y2, precision: 9);
        Assert.Equal(10.0, x0, precision: 3);
    }

    [Fact]
    public void Drifting_measurement_smooths_toward_track()
    {
        KalmanFilterState? s = null;
        var t = T(0);
        double x = 0, y = 0;
        for (var i = 0; i < 40; i++)
        {
            t = t.AddSeconds(1);
            (s, x, y, _) = KalmanFilter2DMath.Update(s, 1.0 * i, 0.0, t, Q, R);
        }
        Assert.True(x > 30, $"x={x}");
        Assert.InRange(y, -1.0, 1.0);
    }
}
