using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackBoard.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "circuits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    length_meters = table.Column<int>(type: "integer", nullable: false),
                    turns = table.Column<int>(type: "integer", nullable: false),
                    elevation_change_meters = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_circuits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "points_schemes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fastest_lap_bonus = table.Column<int>(type: "integer", nullable: false),
                    pole_position_bonus = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_points_schemes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "points_scheme_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    points_scheme_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_points_scheme_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_points_scheme_entries_points_schemes_points_scheme_id",
                        column: x => x.points_scheme_id,
                        principalTable: "points_schemes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    season = table.Column<int>(type: "integer", nullable: false),
                    points_scheme_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series", x => x.id);
                    table.ForeignKey(
                        name: "fk_series_points_schemes_points_scheme_id",
                        column: x => x.points_scheme_id,
                        principalTable: "points_schemes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    engine_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    horsepower = table.Column<int>(type: "integer", nullable: false),
                    torque = table.Column<int>(type: "integer", nullable: true),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    top_speed = table.Column<double>(type: "double precision", nullable: true),
                    acceleration = table.Column<double>(type: "double precision", nullable: true),
                    drivetrain = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fuel_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tire_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fuel_capacity = table.Column<double>(type: "double precision", nullable: true),
                    transmission = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    suspension_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicles_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "race_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    series_id = table.Column<Guid>(type: "uuid", nullable: false),
                    circuit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    laps = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_race_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_race_events_circuits_circuit_id",
                        column: x => x.circuit_id,
                        principalTable: "circuits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_race_events_series_series_id",
                        column: x => x.series_id,
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    race_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: true),
                    did_not_finish = table.Column<bool>(type: "boolean", nullable: false),
                    total_time_ms = table.Column<long>(type: "bigint", nullable: true),
                    best_lap_time_ms = table.Column<long>(type: "bigint", nullable: true),
                    set_fastest_lap = table.Column<bool>(type: "boolean", nullable: false),
                    started_from_pole = table.Column<bool>(type: "boolean", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_results_race_events_race_event_id",
                        column: x => x.race_event_id,
                        principalTable: "race_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_results_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_results_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_circuits_country",
                table: "circuits",
                column: "country");

            migrationBuilder.CreateIndex(
                name: "ix_circuits_name",
                table: "circuits",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_points_scheme_entries_points_scheme_id_position",
                table: "points_scheme_entries",
                columns: new[] { "points_scheme_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_points_schemes_name",
                table: "points_schemes",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_race_events_circuit_id",
                table: "race_events",
                column: "circuit_id");

            migrationBuilder.CreateIndex(
                name: "ix_race_events_series_id",
                table: "race_events",
                column: "series_id");

            migrationBuilder.CreateIndex(
                name: "ix_race_events_series_id_scheduled_at",
                table: "race_events",
                columns: new[] { "series_id", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_results_race_event_id_position",
                table: "results",
                columns: new[] { "race_event_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_results_race_event_id_user_id",
                table: "results",
                columns: new[] { "race_event_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_results_user_id",
                table: "results",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_results_vehicle_id",
                table: "results",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_series_name_season",
                table: "series",
                columns: new[] { "name", "season" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_series_points_scheme_id",
                table: "series",
                column: "points_scheme_id");

            migrationBuilder.CreateIndex(
                name: "ix_series_season",
                table: "series",
                column: "season");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_manufacturer_model",
                table: "vehicles",
                columns: new[] { "manufacturer", "model" });

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_owner_id",
                table: "vehicles",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "points_scheme_entries");

            migrationBuilder.DropTable(
                name: "results");

            migrationBuilder.DropTable(
                name: "race_events");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "circuits");

            migrationBuilder.DropTable(
                name: "series");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "points_schemes");
        }
    }
}
