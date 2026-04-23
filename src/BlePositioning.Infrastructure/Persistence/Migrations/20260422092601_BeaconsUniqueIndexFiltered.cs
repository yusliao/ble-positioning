using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlePositioning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BeaconsUniqueIndexFiltered : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_beacons_uuid_major_minor",
                table: "beacons");

            migrationBuilder.CreateIndex(
                name: "ix_beacons_uuid_major_minor",
                table: "beacons",
                columns: new[] { "uuid", "major", "minor" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_beacons_uuid_major_minor",
                table: "beacons");

            migrationBuilder.CreateIndex(
                name: "ix_beacons_uuid_major_minor",
                table: "beacons",
                columns: new[] { "uuid", "major", "minor" },
                unique: true);
        }
    }
}
