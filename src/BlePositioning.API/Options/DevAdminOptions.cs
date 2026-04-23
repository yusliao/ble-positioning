using BlePositioning.Application.Security;

namespace BlePositioning.API.Options;

public sealed class DevAdminUserEntry
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = BlePositioningRoles.Viewer;
}

public sealed class DevAdminOptions
{
    public const string SectionName = "DevAdmin";

    /// <summary>当 <see cref="Users"/> 为空时用于登录的单一开发账号，角色为 <see cref="BlePositioningRoles.Admin"/>。</summary>
    public string Username { get; set; } = "admin";

    public string Password { get; set; } = "ChangeMe!";

    /// <summary>非空时仅使用本列表做登录（忽略 <see cref="Username"/> / <see cref="Password"/>）。</summary>
    public List<DevAdminUserEntry> Users { get; set; } = [];
}
