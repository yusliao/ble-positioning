namespace BlePositioning.API.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>每设备 RSSI 滑动窗口内最大请求数（默认 10/s，见 design-spec §4.2）。</summary>
    public int RssiPermitLimit { get; set; } = 10;

    public int RssiWindowSeconds { get; set; } = 1;

    /// <summary>普通 API 固定窗口内最大请求数（默认 60/min）。</summary>
    public int GeneralPermitLimit { get; set; } = 60;

    public int GeneralWindowMinutes { get; set; } = 1;
}
