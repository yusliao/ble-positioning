namespace BlePositioning.Application.Positioning;

/// <summary>将质心量测做 2D 卡尔曼平滑（与 <see cref="KalmanFilter2DMath"/> 配置一致）。</summary>
public interface IKalmanPositionFilter
{
    Task<(double X, double Y, double Accuracy)> SmoothAsync(
        Guid deviceId,
        double measX,
        double measY,
        DateTime measuredAtUtc,
        CancellationToken ct = default);
}
