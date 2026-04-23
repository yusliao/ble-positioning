namespace BlePositioning.Infrastructure.Options;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    /// <summary>与明文 API Key 拼接后做 SHA256；生产必须非空。</summary>
    public string Pepper { get; set; } = "";
}
