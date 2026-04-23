using BlePositioning.Application.Devices;

namespace BlePositioning.Tests;

/// <summary>InMemory 测试宿主无真实 position_logs 表；避免对 Npgsql 执行原始 SQL。</summary>
internal sealed class EmptyTrajectoryQuery : ITrajectoryQuery
{
    public Task<IReadOnlyList<TrajectoryPointDto>> GetPointsAsync(
        Guid deviceId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? floorId,
        int intervalSeconds,
        int maxPoints,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TrajectoryPointDto>>([]);
}
