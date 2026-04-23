namespace BlePositioning.Application.Positioning;

/// <summary>二维位置随机游走进退：预测 <c>P += Q*dt*I</c>，量测 <c>H=I，R=r*I</c>。纯函数，单元测试可复现。</summary>
public static class KalmanFilter2DMath
{
    public static (KalmanFilterState State, double OutX, double OutY, double OutAccuracy) Update(
        KalmanFilterState? prior,
        double zx,
        double zy,
        DateTime tUtc,
        double processNoiseQ,
        double measurementNoiseR)
    {
        if (tUtc.Kind != DateTimeKind.Utc)
            tUtc = tUtc.ToUniversalTime();
        if (processNoiseQ < 0 || measurementNoiseR <= 0)
            throw new ArgumentOutOfRangeException("noise");

        if (prior is null)
        {
            var s0 = KalmanFilterState.Initial(zx, zy, tUtc);
            return (s0, zx, zy, Math.Sqrt(s0.P00 + s0.P11));
        }

        var dt = Math.Clamp((tUtc - prior.LastUpdateUtc).TotalSeconds, 0.01, 20.0);
        var qd = processNoiseQ * Math.Max(0.001, dt);
        // P' = P + Q, Q 标量*I
        var p00 = prior.P00 + qd;
        var p11 = prior.P11 + qd;
        var p01 = prior.P01;
        var p10 = prior.P10;

        // S = P' + R*I, K = P' * S^{-1}, x = x' + K (z - x')
        var s00 = p00 + measurementNoiseR;
        var s11 = p11 + measurementNoiseR;
        Invert2x2Symmetric(s00, p01, p10, s11, out var si00, out var si01, out var si10, out var si11);

        var k00 = p00 * si00 + p01 * si10;
        var k01 = p00 * si01 + p01 * si11;
        var k10 = p10 * si00 + p11 * si10;
        var k11 = p10 * si01 + p11 * si11;

        var yx = zx - prior.X;
        var yy = zy - prior.Y;
        var nx = prior.X + k00 * yx + k01 * yy;
        var ny = prior.Y + k10 * yx + k11 * yy;

        // P = (I - K) P' with H=I, K=2x2, P2=(I-K)P' 
        // I - K
        var i00 = 1 - k00;
        var i01 = -k01;
        var i10 = -k10;
        var i11 = 1 - k11;
        // P2 = (I-K)*P'
        var n00 = i00 * p00 + i01 * p10;
        var n01 = i00 * p01 + i01 * p11;
        var n10 = i10 * p00 + i11 * p10;
        var n11 = i10 * p01 + i11 * p11;

        var next = new KalmanFilterState
        {
            X = nx,
            Y = ny,
            P00 = n00,
            P01 = n01,
            P10 = n10,
            P11 = n11,
            LastUpdateUtc = tUtc,
        };
        return (next, nx, ny, Math.Sqrt(Math.Max(0, n00 + n11)));
    }

    private static void Invert2x2Symmetric(
        double a, double b, double c, double d,
        out double o00, out double o01, out double o10, out double o11)
    {
        var det = a * d - b * c;
        if (Math.Abs(det) < 1e-15)
        {
            o00 = o11 = 1.0;
            o01 = o10 = 0.0;
            return;
        }
        o00 = d / det;
        o11 = a / det;
        o01 = -b / det;
        o10 = -c / det;
    }
}
