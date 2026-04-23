using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlePositioning.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPositionLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE position_logs (
                id          bigserial,
                device_id   uuid NOT NULL,
                floor_id    uuid NOT NULL,
                x           numeric(10,2) NOT NULL,
                y           numeric(10,2) NOT NULL,
                accuracy    numeric(5,2) NOT NULL,
                "timestamp" timestamptz NOT NULL,
                PRIMARY KEY ("timestamp", id)
            ) PARTITION BY RANGE ("timestamp");

            CREATE TABLE position_logs_default PARTITION OF position_logs DEFAULT;

            CREATE INDEX ix_position_logs_device_ts
                ON position_logs (device_id, "timestamp" DESC)
                INCLUDE (x, y, accuracy, floor_id);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS position_logs CASCADE;");
    }
}
