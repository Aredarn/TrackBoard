using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackBoard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackProSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    length_meters = table.Column<double>(type: "double precision", nullable: true),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_latitude = table.Column<double>(type: "double precision", nullable: true),
                    start_longitude = table.Column<double>(type: "double precision", nullable: true),
                    sector_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tracks", x => x.id);
                    table.ForeignKey(
                        name: "fk_tracks_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    track_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gps_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    voided = table.Column<bool>(type: "boolean", nullable: false),
                    app_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    weather_temp_c = table.Column<double>(type: "double precision", nullable: true),
                    weather_humidity_pct = table.Column<int>(type: "integer", nullable: true),
                    weather_precipitation_mm = table.Column<double>(type: "double precision", nullable: true),
                    weather_code = table.Column<int>(type: "integer", nullable: true),
                    weather_wind_kph = table.Column<double>(type: "double precision", nullable: true),
                    weather_wind_dir_deg = table.Column<int>(type: "integer", nullable: true),
                    weather_pressure_hpa = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_sessions_tracks_track_id",
                        column: x => x.track_id,
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_sessions_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sessions_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "track_points",
                columns: table => new
                {
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    altitude = table.Column<double>(type: "double precision", nullable: true),
                    is_start_point = table.Column<bool>(type: "boolean", nullable: false),
                    is_sector_point = table.Column<bool>(type: "boolean", nullable: false),
                    sector_index = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_track_points", x => new { x.track_id, x.seq });
                    table.ForeignKey(
                        name: "fk_track_points_tracks_track_id",
                        column: x => x.track_id,
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "laps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lap_number = table.Column<int>(type: "integer", nullable: false),
                    time_ms = table.Column<int>(type: "integer", nullable: false),
                    signal_gap = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_laps", x => x.id);
                    table.ForeignKey(
                        name: "fk_laps_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lap_sectors",
                columns: table => new
                {
                    lap_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sector_index = table.Column<int>(type: "integer", nullable: false),
                    split_ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lap_sectors", x => new { x.lap_id, x.sector_index });
                    table.ForeignKey(
                        name: "fk_lap_sectors_laps_lap_id",
                        column: x => x.lap_id,
                        principalTable: "laps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_laps_session_id_lap_number",
                table: "laps",
                columns: new[] { "session_id", "lap_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_owner_id_started_at",
                table: "sessions",
                columns: new[] { "owner_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sessions_track_id_visibility_voided",
                table: "sessions",
                columns: new[] { "track_id", "visibility", "voided" });

            migrationBuilder.CreateIndex(
                name: "ix_sessions_vehicle_id",
                table: "sessions",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_owner_id",
                table: "tracks",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_visibility_start_latitude_start_longitude",
                table: "tracks",
                columns: new[] { "visibility", "start_latitude", "start_longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lap_sectors");

            migrationBuilder.DropTable(
                name: "track_points");

            migrationBuilder.DropTable(
                name: "laps");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "tracks");
        }
    }
}
