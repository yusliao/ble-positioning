namespace BlePositioning.Infrastructure.Options;

/// <summary>设备在线/离线边沿与后台扫频（与 <see cref="PositioningOptions.PositionTtlSeconds"/> 组合定义 «在线»：见 api-conventions）。</summary>
public sealed class DevicePresenceOptions
{
    public const string SectionName = "DevicePresence";

    /// <summary>Redis 中 <c>dpl:&#123;deviceId&#125;</c> 生命状态 <c>on|off</c> 的过期时间，需长于定位管道不活跃窗，使扫频能区分 «从未上线» 与 «已离线»。</summary>
    public TimeSpan StateKeyTtl { get; set; } = TimeSpan.FromDays(7);

    /// <summary>检查 pos 键是否过期的周期间隔，可调小以更快落离线（代价：Redis/CPU）。</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>单设备一次查询返回条数上限（与进区事件查询同量级）。</summary>
    public int QueryMaxEvents { get; set; } = 10_000;
}
