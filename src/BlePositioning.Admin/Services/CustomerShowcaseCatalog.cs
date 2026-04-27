namespace BlePositioning.Admin.Services;

/// <summary>客户/培训向展示文档（源文件在仓库 <c>doc/</c> 或根 <c>README</c>，通过 csproj 链接到 <c>wwwroot/customer-showcase</c>）。</summary>
public static class CustomerShowcaseCatalog
{
    public sealed record Entry(string Slug, string FileName, string Title, string Summary, bool PrimaryDemo);

    public static IReadOnlyList<Entry> Documents { get; } =
    [
        new(
            "demo-runbook",
            "demo-runbook.md",
            "客户演示 Runbook",
            "会议主线 15～20 分钟 + 可选扩展时间盒；演示前环境与排障。",
            PrimaryDemo: true),
        new(
            "customer-learning",
            "customer-learning.md",
            "客户学习与动手实践",
            "分步练习、JWT/角色、Swagger、RSSI→位姿、自测表。",
            PrimaryDemo: false),
        new(
            "demo-rehearsal-checklist",
            "demo-rehearsal-checklist.md",
            "演示彩排清单",
            "口播与计时：主线 / 扩展分表，与 Runbook 配套。",
            PrimaryDemo: false),
        new(
            "demo-seed",
            "demo-seed.md",
            "演示种子数据说明",
            "坐标约定、手填示例（无密钥）。",
            PrimaryDemo: false),
        new(
            "project-readme",
            "project-readme.md",
            "项目 README（启动与脚本）",
            "本地与 Docker 启动、端口、脚本入口。",
            PrimaryDemo: false),
    ];

    public static Entry? FindBySlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;
        foreach (var e in Documents)
        {
            if (string.Equals(e.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase))
                return e;
        }
        return null;
    }
}
