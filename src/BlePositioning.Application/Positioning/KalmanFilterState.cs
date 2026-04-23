using System.Text.Json.Serialization;

namespace BlePositioning.Application.Positioning;

/// <summary>2D 位置子空间卡尔曼状态（2×2 协方差），与 Redis 键 <c>device:&#123;id&#125;:kalman</c> 序列化一致。</summary>
public sealed class KalmanFilterState
{
    [JsonPropertyName("x")]
    public double X { get; set; }
    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("p00")]
    public double P00 { get; set; }
    [JsonPropertyName("p01")]
    public double P01 { get; set; }
    [JsonPropertyName("p10")]
    public double P10 { get; set; }
    [JsonPropertyName("p11")]
    public double P11 { get; set; }

    [JsonPropertyName("lastUpdateUtc")]
    public DateTime LastUpdateUtc { get; set; }

    public static KalmanFilterState Initial(double mx, double my, DateTime tUtc) =>
        new()
        {
            X = mx,
            Y = my,
            P00 = 100,
            P11 = 100,
            P01 = 0,
            P10 = 0,
            LastUpdateUtc = tUtc,
        };
}
