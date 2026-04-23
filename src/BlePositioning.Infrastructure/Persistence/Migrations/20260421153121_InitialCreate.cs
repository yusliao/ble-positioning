using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlePositioning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "floors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    building_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    width_meters = table.Column<double>(type: "numeric(10,2)", nullable: false),
                    height_meters = table.Column<double>(type: "numeric(10,2)", nullable: false),
                    map_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tracked_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    api_key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    api_key_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tracked_devices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    zone_polygon = table.Column<string>(type: "text", nullable: false),
                    trigger_on = table.Column<short>(type: "smallint", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alert_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_alert_rules_floors_floor_id",
                        column: x => x.floor_id,
                        principalTable: "floors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "beacons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uuid = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    major = table.Column<int>(type: "integer", nullable: false),
                    minor = table.Column<int>(type: "integer", nullable: false),
                    x = table.Column<double>(type: "numeric(10,2)", nullable: false),
                    y = table.Column<double>(type: "numeric(10,2)", nullable: false),
                    tx_power = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_beacons", x => x.id);
                    table.ForeignKey(
                        name: "fk_beacons_floors_floor_id",
                        column: x => x.floor_id,
                        principalTable: "floors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_floor_id",
                table: "alert_rules",
                column: "floor_id");

            migrationBuilder.CreateIndex(
                name: "ix_beacons_floor_id",
                table: "beacons",
                column: "floor_id");

            migrationBuilder.CreateIndex(
                name: "ix_beacons_uuid_major_minor",
                table: "beacons",
                columns: new[] { "uuid", "major", "minor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tracked_devices_api_key_hash",
                table: "tracked_devices",
                column: "api_key_hash",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_tracked_devices_device_code",
                table: "tracked_devices",
                column: "device_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_rules");

            migrationBuilder.DropTable(
                name: "beacons");

            migrationBuilder.DropTable(
                name: "tracked_devices");

            migrationBuilder.DropTable(
                name: "floors");
        }
    }
}
