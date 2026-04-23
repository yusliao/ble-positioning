using BlePositioning.Application.Positioning;

namespace BlePositioning.Infrastructure.Options;

public sealed class PositioningOptions
{
    public const string SectionName = "Positioning";

    public int DefaultTxPower { get; set; } = -59;
    public double PathLossExponent { get; set; } = 2.0;
    public int MinBeaconsRequired { get; set; } = 3;
    public int RssiAggregationWindowMs { get; set; } = 300;
    /// <summary>Redis <c>pos:&#123;deviceId&#125;</c> TTL（秒）；与 «在线» 判定一致，见 api-conventions §12。</summary>
    public int PositionTtlSeconds { get; set; } = 60;

    /// <summary>为 <c>true</c> 时用 2D 卡尔曼（<see cref="KalmanFilter2DMath"/>）替代原 α 一阶低通；见 ADR-008 与 <c>StoreKalmanStateInRedis</c>。</summary>
    public bool UseKalmanFilter { get; set; }

    /// <summary>为 <c>true</c> 时卡尔曼状态写入 Redis 键 <c>device:&#123;id&#125;:kalman</c>；<c>false</c> 时进程内字典（单实例部署）。</summary>
    public bool StoreKalmanStateInRedis { get; set; }

    public double KalmanProcessNoise { get; set; } = 0.05;
    public double KalmanMeasurementNoise { get; set; } = 4.0;
    public int KalmanStateTtlSeconds { get; set; } = 300;
}
