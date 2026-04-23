using BlePositioning.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace BlePositioning.Infrastructure.Persistence.BulkWriters;

public sealed class NpgsqlTrajectoryBulkWriter(
    IConfiguration configuration,
    ILogger<NpgsqlTrajectoryBulkWriter> logger) : ITrajectoryBulkWriter
{
    private readonly string _connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

    public async Task WriteBatchAsync(IReadOnlyList<PositionLogRow> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0)
            return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await using var importer = await conn.BeginBinaryImportAsync(
                """
                COPY position_logs (device_id, floor_id, x, y, accuracy, "timestamp")
                FROM STDIN (FORMAT BINARY)
                """,
                ct);

            foreach (var row in rows)
            {
                await importer.StartRowAsync(ct);
                await importer.WriteAsync(row.DeviceId, NpgsqlDbType.Uuid, ct);
                await importer.WriteAsync(row.FloorId, NpgsqlDbType.Uuid, ct);
                await importer.WriteAsync(ToNumeric(row.X), NpgsqlDbType.Numeric, ct);
                await importer.WriteAsync(ToNumeric(row.Y), NpgsqlDbType.Numeric, ct);
                await importer.WriteAsync(ToNumeric(row.Accuracy), NpgsqlDbType.Numeric, ct);
                var ts = row.TimestampUtc.Kind == DateTimeKind.Utc
                    ? row.TimestampUtc
                    : row.TimestampUtc.ToUniversalTime();
                await importer.WriteAsync(ts, NpgsqlDbType.TimestampTz, ct);
            }

            await importer.CompleteAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            logger.LogError(ex, "Trajectory bulk write failed for batch of {Count} rows", rows.Count);
            throw;
        }
    }

    private static decimal ToNumeric(double value) => (decimal)value;
}
