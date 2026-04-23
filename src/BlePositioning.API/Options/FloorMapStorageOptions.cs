namespace BlePositioning.API.Options;

public sealed class FloorMapStorageOptions
{
    public const string SectionName = "FloorMapStorage";

    /// <summary>单文件最大字节数（默认 6 MB）。</summary>
    public long MaxBytes { get; set; } = 6_000_000;

    /// <summary>相对于 wwwroot 的子目录（URL 为 /{WebRelativeFolder}/...）。</summary>
    public string WebRelativeFolder { get; set; } = "maps";

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
    ];
}
