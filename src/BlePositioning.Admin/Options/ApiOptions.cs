namespace BlePositioning.Admin.Options;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>服务端 HttpClient 基址，例如 <c>http://localhost:5230</c> 或容器内 <c>http://api:8080</c>。</summary>
    public string Base { get; set; } = "http://localhost:5230";

    /// <summary>
    /// 浏览器可访问的 API 根 URL（宿主机端口等）。Docker 中 <see cref="Base"/> 常为容器名，Swagger/地图 &lt;img&gt; 无法解析，此时应配置此项。
    /// </summary>
    public string? PublicBase { get; set; }

    /// <summary>用于 href、&lt;img src&gt; 等由浏览器发起的请求；未配置 <see cref="PublicBase"/> 时回退到 <see cref="Base"/>。</summary>
    public string GetBrowserApiRoot()
    {
        var pub = PublicBase?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(pub))
            return pub;
        return Base?.Trim().TrimEnd('/') ?? "http://localhost:5230";
    }
}
