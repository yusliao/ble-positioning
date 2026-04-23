namespace BlePositioning.Application.Devices;

public interface ITrajectoryQuery
{
    /// <summary>
    /// 按时间桶聚合轨迹点；调用方负责 interval 与桶数上限校验。
    /// 若命中超过 <paramref name="maxPoints"/> 行则只返回前 <paramref name="maxPoints"/> 条（由 SQL LIMIT 约束），
    /// 调用方应检测数量是否等于 maxPoints+1 以返回 400。
    /// </summary>
    Task<IReadOnlyList<TrajectoryPointDto>> GetPointsAsync(
        Guid deviceId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? floorId,
        int intervalSeconds,
        int maxPoints,
        CancellationToken ct = default);
}
