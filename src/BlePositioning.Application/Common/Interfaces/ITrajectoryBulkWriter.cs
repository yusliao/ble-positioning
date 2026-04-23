namespace BlePositioning.Application.Common.Interfaces;

public readonly record struct PositionLogRow(
    Guid DeviceId,
    Guid FloorId,
    double X,
    double Y,
    double Accuracy,
    DateTime TimestampUtc);

public interface ITrajectoryBulkWriter
{
    Task WriteBatchAsync(IReadOnlyList<PositionLogRow> rows, CancellationToken ct = default);
}
