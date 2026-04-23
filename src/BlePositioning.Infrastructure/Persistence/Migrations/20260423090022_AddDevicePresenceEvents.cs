using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlePositioning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDevicePresenceEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_presence_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_kind = table.Column<short>(type: "smallint", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_presence_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_presence_events_device_id_occurred_at_utc",
                table: "device_presence_events",
                columns: new[] { "device_id", "occurred_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_presence_events");
        }
    }
}
