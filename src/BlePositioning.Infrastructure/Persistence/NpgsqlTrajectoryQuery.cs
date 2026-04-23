using BlePositioning.Application.Devices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace BlePositioning.Infrastructure.Persistence;

public sealed class NpgsqlTrajectoryQuery(IDbContextFactory<AppDbContext> dbFactory) : ITrajectoryQuery
{
    public async Task<IReadOnlyList<TrajectoryPointDto>> GetPointsAsync(
        Guid deviceId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? floorId,
        int intervalSeconds,
        int maxPoints,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(
            """
            SELECT
              floor_id,
              round(avg(x::numeric), 2)::float8 AS ax,
              round(avg(y::numeric), 2)::float8 AS ay,
              min("timestamp") AS ts
            FROM position_logs
            WHERE device_id = @device_id
              AND "timestamp" >= @start_utc
              AND "timestamp" <= @end_utc
              AND (@floor_id IS NULL OR floor_id = @floor_id)
            GROUP BY (extract(epoch from "timestamp")::bigint / @interval_secs), floor_id
            ORDER BY min("timestamp")
            LIMIT @limit
            """,
            conn);

        cmd.Parameters.Add(new NpgsqlParameter("device_id", deviceId));
        cmd.Parameters.Add(new NpgsqlParameter("start_utc", DateTime.SpecifyKind(startUtc, DateTimeKind.Utc)));
        cmd.Parameters.Add(new NpgsqlParameter("end_utc", DateTime.SpecifyKind(endUtc, DateTimeKind.Utc)));
        cmd.Parameters.Add(new NpgsqlParameter("floor_id", NpgsqlDbType.Uuid)
        {
            Value = floorId.HasValue ? floorId.Value : DBNull.Value,
        });
        cmd.Parameters.Add(new NpgsqlParameter("interval_secs", intervalSeconds));
        cmd.Parameters.Add(new NpgsqlParameter("limit", maxPoints));

        var list = new List<TrajectoryPointDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var floor = reader.GetGuid(0);
            var x = reader.GetDouble(1);
            var y = reader.GetDouble(2);
            var ts = reader.GetFieldValue<DateTime>(3);
            list.Add(new TrajectoryPointDto(x, y, floor, DateTime.SpecifyKind(ts, DateTimeKind.Utc)));
        }

        return list;
    }
}
