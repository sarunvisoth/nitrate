using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackHistoryEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "skip_count",
                table: "entity_playback",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "entity_playback_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    position_seconds = table.Column<double>(type: "double precision", nullable: true),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_playback_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_entity_playback_events_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entity_playback_events_entity_id_occurred_at",
                table: "entity_playback_events",
                columns: new[] { "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_entity_playback_events_kind_occurred_at",
                table: "entity_playback_events",
                columns: new[] { "kind", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entity_playback_events");

            migrationBuilder.DropColumn(
                name: "skip_count",
                table: "entity_playback");
        }
    }
}
