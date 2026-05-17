namespace BlePositioning.API.Options;

public sealed class SecurityHeadersOptions
{
    public const string SectionName = "SecurityHeaders";

    public bool Enabled { get; set; } = true;

    /// <summary>HTTPS 响应附加 Strict-Transport-Security（生产建议开启）。</summary>
    public bool UseHsts { get; set; }

    public int HstsMaxAgeSeconds { get; set; } = 31_536_000;
}
