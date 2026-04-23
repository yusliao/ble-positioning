using BlePositioning.Application.Common.Interfaces;

namespace BlePositioning.Infrastructure.Persistence;

public sealed class NoOpTrajectoryBulkWriter : ITrajectoryBulkWriter
{
    public Task WriteBatchAsync(IReadOnlyList<PositionLogRow> rows, CancellationToken ct = default) =>
        Task.CompletedTask;
}
