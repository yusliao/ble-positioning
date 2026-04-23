using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlePositioning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeofenceEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "geofence_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_kind = table.Column<short>(type: "smallint", nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                    accuracy = table.Column<double>(type: "double precision", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_geofence_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_geofence_events_alert_rules_alert_rule_id",
                        column: x => x.alert_rule_id,
                        principalTable: "alert_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_geofence_events_floors_floor_id",
                        column: x => x.floor_id,
                        principalTable: "floors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_geofence_events_tracked_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "tracked_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_geofence_events_alert_rule_id",
                table: "geofence_events",
                column: "alert_rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_geofence_events_device_id_occurred_at_utc",
                table: "geofence_events",
                columns: new[] { "device_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_geofence_events_floor_id",
                table: "geofence_events",
                column: "floor_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "geofence_events");
        }
    }
}
