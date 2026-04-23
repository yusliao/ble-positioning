namespace BlePositioning.Application.Geofence;

/// <summary>按 (设备, 规则) 记录<strong>上一时刻</strong>点是否在多边形内（无键视为 false）。</summary>
public interface IGeofenceStateStore
{
    /// <summary>无记录时返回 <c>false</c>（认为上一时刻在区外），以便首次进入可产生进入事件。</summary>
    Task<bool> GetWasInsideAsync(Guid deviceId, Guid ruleId, CancellationToken ct = default);

    Task SetWasInsideAsync(Guid deviceId, Guid ruleId, bool wasInside, CancellationToken ct = default);
}
