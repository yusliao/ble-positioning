using BlePositioning.Application.Floors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BlePositioning.API.Hubs;

/// <summary>实时位置订阅；分组为 <c>floor:{floorId}</c>（见 doc/CLAUDE.md）。服务器还会向同组推送 <c>PositionUpdated</c> 与 <c>GeofenceEvent</c>，并向全部连接广播 <c>DevicePresenceEvent</c>（参数见 <c>api-conventions.md</c> §8 / §10 / §12）。</summary>
[Authorize]
public sealed class PositioningHub(
    IServiceScopeFactory scopeFactory,
    ILogger<PositioningHub> logger) : Hub
{
    public static string FloorGroupName(Guid floorId) => $"floor:{floorId}";

    public async Task JoinFloor(Guid floorId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var floors = scope.ServiceProvider.GetRequiredService<IFloorRepository>();
        if (!await floors.ExistsActiveAsync(floorId, Context.ConnectionAborted))
            throw new HubException("楼层不存在或已删除。");

        await Groups.AddToGroupAsync(Context.ConnectionId, FloorGroupName(floorId), Context.ConnectionAborted);
        logger.LogDebug("Client {ConnectionId} joined {FloorId}", Context.ConnectionId, floorId);
    }

    public async Task LeaveFloor(Guid floorId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, FloorGroupName(floorId), Context.ConnectionAborted);
        logger.LogDebug("Client {ConnectionId} left {FloorId}", Context.ConnectionId, floorId);
    }
}
