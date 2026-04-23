namespace BlePositioning.Infrastructure.Geofence;

/// <summary>进/出区事件 Webhook 出站（阶段 D）。未配置 <see cref="Url"/> 或 <see cref="Enabled"/> 为 false 时不发送。</summary>
public sealed class GeofenceWebhookOptions
{
    public const string SectionName = "GeofenceWebhook";

    /// <summary>是否启用；默认关，避免未配置 URL 时误发。</summary>
    public bool Enabled { get; set; }

    /// <summary>完整 HTTPS/HTTP 目标地址（含路径）。</summary>
    public string? Url { get; set; }

    /// <summary>非空时：对 **UTF-8 请求体** 做 HMAC-SHA256，头 <see cref="SignatureHeaderName"/> 值为 <c>sha256=</c> + 小写十六进制。</summary>
    public string? Secret { get; set; }

    public string SignatureHeaderName { get; set; } = "X-Ble-Webhook-Signature";

    public string SchemaVersion { get; set; } = "1.0";

    /// <summary>单次请求超时（非“总重试时长”）。</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>最大尝试次数，含第一次请求（如 3 即最多 1+2 次重试间等待）。</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>重试前等待，指数回退：第 n 次失败后等待 <c>BaseDelay * 2^n</c>，上限 30s。</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(400);
}
