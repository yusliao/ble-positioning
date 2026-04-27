using Microsoft.AspNetCore.Hosting;

namespace BlePositioning.Admin.Services;

/// <summary>
/// 解析 <c>customer-showcase</c> 目录：本地 <c>dotnet run</c> 时文件在 <c>bin/.../wwwroot</c>，
/// 而 <see cref="IWebHostEnvironment.WebRootPath"/> 常指向项目下 <c>wwwroot</c>，需多路径探测。
/// </summary>
public static class CustomerShowcasePathResolver
{
    public static string? ResolveShowcaseDirectory(IWebHostEnvironment env)
    {
        var candidates = new List<string>(3);
        if (!string.IsNullOrEmpty(env.WebRootPath))
            candidates.Add(Path.Combine(env.WebRootPath, "customer-showcase"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "wwwroot", "customer-showcase"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "customer-showcase"));

        foreach (var c in candidates)
        {
            try
            {
                var full = Path.GetFullPath(c);
                if (Directory.Exists(full))
                    return full;
            }
            catch (ArgumentException)
            {
                // 忽略非法路径
            }
        }

        return null;
    }
}
