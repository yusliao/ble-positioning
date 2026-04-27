namespace BlePositioning.Admin.Services;

/// <summary>集中判断哪些路由无需 JWT，以及登录后 returnUrl 是否安全。</summary>
public static class AdminRouteGuard
{
    public static bool IsPublicRelativePath(string relativePath)
    {
        var p = relativePath.Trim().TrimStart('/').TrimEnd('/');
        return p.Equals("login", StringComparison.OrdinalIgnoreCase)
            || p.Equals("Error", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>仅允许站内相对路径，防止开放重定向。</summary>
    public static bool IsSafeReturnUrl(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;
        var t = value.Trim();
        if (t.Contains("..", StringComparison.Ordinal))
            return false;
        if (t.Contains('\\', StringComparison.Ordinal))
            return false;
        if (t.Contains("//", StringComparison.Ordinal))
            return false;
        if (t.StartsWith("//", StringComparison.Ordinal))
            return false;
        if (t.Contains(':', StringComparison.Ordinal))
            return false;
        return true;
    }
}
