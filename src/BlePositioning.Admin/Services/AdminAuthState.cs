using BlePositioning.Application.Security;

namespace BlePositioning.Admin.Services;

/// <summary>Blazor Server 电路内的 JWT（内存），仅供连接 BlePositioning API 使用。</summary>
public sealed class AdminAuthState
{
    public string? BearerToken { get; set; }

    /// <summary>与登录响应中 <c>role</c> 一致；未设置时视为可管理（兼容旧 API）。</summary>
    public string? Role { get; set; }

    public bool IsAdmin =>
        string.IsNullOrEmpty(Role) ||
        string.Equals(Role, BlePositioningRoles.Admin, StringComparison.Ordinal);

    public void Clear()
    {
        BearerToken = null;
        Role = null;
    }
}
