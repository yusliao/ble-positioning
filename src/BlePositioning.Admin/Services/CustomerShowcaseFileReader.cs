using System.Text;

namespace BlePositioning.Admin.Services;

/// <summary>读取客户展示 Markdown：优先 UTF-8（含 BOM），严格解码失败时回退 GB18030（常见于中文 Windows 另存编码）。</summary>
public static class CustomerShowcaseFileReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return DecodeFromBytes(bytes);
    }

    private static string DecodeFromBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var span = bytes.AsSpan();

        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            return DecodeUtf8OrGb18030(span.Slice(3));

        if (span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFE)
            return Encoding.Unicode.GetString(span.Slice(2));

        if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(span.Slice(2));

        return DecodeUtf8OrGb18030(span);
    }

    private static string DecodeUtf8OrGb18030(ReadOnlySpan<byte> span)
    {
        try
        {
            return StrictUtf8.GetString(span);
        }
        catch (DecoderFallbackException)
        {
            try
            {
                return Encoding.GetEncoding("GB18030").GetString(span);
            }
            catch (ArgumentException)
            {
                return Encoding.UTF8.GetString(span);
            }
        }
    }
}
