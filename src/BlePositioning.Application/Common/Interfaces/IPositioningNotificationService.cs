namespace BlePositioning.Application.Common.Interfaces;

/// <summary>实时位置推送（例如 SignalR），由 Host 项目注册具体实现。</summary>
public interface IPositioningNotificationService
{
    /// <param name="floorId">接收分组 <c>floor:{floorId}</c> 的推送。</param>
    /// <remarks>与客户端约定 <c>PositionUpdated</c> 参数顺序：<c>deviceId, x, y, accuracy, floorId</c>。</remarks>
    Task NotifyPositionUpdatedAsync(
        Guid floorId,
        Guid deviceId,
        double x,
        double y,
        double accuracy,
        CancellationToken cancellationToken = default);
}
