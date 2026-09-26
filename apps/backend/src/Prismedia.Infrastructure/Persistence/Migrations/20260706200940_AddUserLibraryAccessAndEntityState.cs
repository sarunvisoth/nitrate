using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Per-user library access and engagement state. Hand-edited from the scaffold so the
    /// data motion runs BEFORE the legacy storage is dropped: every existing user is
    /// granted every existing library, and the pre-multi-user global engagement state
    /// (playback, reading progress, favorite, rating) is copied to every user so all
    /// existing clients see unchanged shelves and resume points after the upgrade.
    /// </summary>
    public partial class AddUserLibraryAccessAndEntityState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "library_roots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "entity_playback_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_entity_states",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    rating_value = table.Column<int>(type: "integer", nullable: true),
                    play_count = table.Column<int>(type: "integer", nullable: false),
                    skip_count = table.Column<int>(type: "integer", nullable: false),
                    play_duration_seconds = table.Column<double>(type: "double precision", nullable: false),
                    resume_seconds = table.Column<double>(type: "double precision", nullable: false),
                    last_played_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    progress_current_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    progress_unit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    progress_index = table.Column<int>(type: "integer", nullable: false),
                    progress_total = table.Column<int>(type: "integer", nullable: false),
                    progress_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    progress_location = table.Column<string>(type: "text", nullable: true),
                    progress_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_entity_states", x => new { x.user_id, x.entity_id });
                    table.CheckConstraint("ck_user_entity_states_progress_bounds", "progress_index >= 0 AND progress_total >= 0");
                    table.ForeignKey(
                        name: "FK_user_entity_states_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_entity_states_entities_progress_current_entity_id",
                        column: x => x.progress_current_entity_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_entity_states_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_library_access",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    library_root_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_library_access", x => new { x.user_id, x.library_root_id });
                    table.ForeignKey(
                        name: "FK_user_library_access_library_roots_library_root_id",
                        column: x => x.library_root_id,
                        principalTable: "library_roots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_library_access_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_library_roots_created_by_user_id",
                table: "library_roots",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_entity_playback_events_user_id_occurred_at",
                table: "entity_playback_events",
                columns: new[] { "user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_user_entity_states_entity_id",
                table: "user_entity_states",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_entity_states_progress_current_entity_id",
                table: "user_entity_states",
                column: "progress_current_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_entity_states_user_id_is_favorite",
                table: "user_entity_states",
                columns: new[] { "user_id", "is_favorite" },
                filter: "is_favorite");

            migrationBuilder.CreateIndex(
                name: "IX_user_entity_states_user_id_last_played_at",
                table: "user_entity_states",
                columns: new[] { "user_id", "last_played_at" },
                filter: "last_played_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_library_access_library_root_id",
                table: "user_library_access",
                column: "library_root_id");

            migrationBuilder.AddForeignKey(
                name: "FK_entity_playback_events_users_user_id",
                table: "entity_playback_events",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_library_roots_users_created_by_user_id",
                table: "library_roots",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Migration default: every existing (migrated) user keeps access to every existing
            // library THEIR PERMISSIONS ALLOW — a profile that could not see NSFW content before the
            // upgrade must never be granted an NSFW library by it. New users start with no grants;
            // admins never need rows.
            migrationBuilder.Sql("""
                INSERT INTO user_library_access (user_id, library_root_id, created_at)
                SELECT u.id, r.id, now()
                FROM users u
                CROSS JOIN library_roots r
                WHERE u.allow_nsfw OR NOT r.is_nsfw;
                """);

            // Fan-out copy: every user gets an identical copy of the global engagement
            // state, limited to entities that carry any engagement at all.
            migrationBuilder.Sql("""
                INSERT INTO user_entity_states (
                    user_id, entity_id, is_favorite, rating_value, play_count, skip_count,
                    play_duration_seconds, resume_seconds, last_played_at, completed_at,
                    progress_current_entity_id, progress_unit, progress_index, progress_total,
                    progress_mode, progress_location, progress_completed_at, updated_at)
                SELECT u.id, e.id, e.is_favorite, e.rating_value,
                    COALESCE(p.play_count, 0), COALESCE(p.skip_count, 0),
                    COALESCE(p.play_duration_seconds, 0), COALESCE(p.resume_seconds, 0),
                    p.last_played_at, p.completed_at,
                    pr.current_entity_id, COALESCE(pr.unit, 'item'), COALESCE(pr."index", 0), COALESCE(pr.total, 0),
                    pr.mode, pr.location, pr.completed_at, now()
                FROM users u
                CROSS JOIN entities e
                LEFT JOIN entity_playback p ON p.entity_id = e.id
                LEFT JOIN entity_progress pr ON pr.entity_id = e.id
                WHERE e.is_favorite
                   OR e.rating_value IS NOT NULL
                   OR p.entity_id IS NOT NULL
                   OR pr.entity_id IS NOT NULL;
                """);

            // Hard cutover: the copied global storage is dropped (hard-delete-only policy).
            migrationBuilder.DropTable(
                name: "entity_playback");

            migrationBuilder.DropTable(
                name: "entity_progress");

            migrationBuilder.DropColumn(
                name: "is_favorite",
                table: "entities");

            migrationBuilder.DropColumn(
                name: "rating_value",
                table: "entities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_entity_playback_events_users_user_id",
                table: "entity_playback_events");

            migrationBuilder.DropForeignKey(
                name: "FK_library_roots_users_created_by_user_id",
                table: "library_roots");

            migrationBuilder.DropTable(
                name: "user_entity_states");

            migrationBuilder.DropTable(
                name: "user_library_access");

            migrationBuilder.DropIndex(
                name: "IX_library_roots_created_by_user_id",
                table: "library_roots");

            migrationBuilder.DropIndex(
                name: "IX_entity_playback_events_user_id_occurred_at",
                table: "entity_playback_events");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "library_roots");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "entity_playback_events");

            migrationBuilder.AddColumn<bool>(
                name: "is_favorite",
                table: "entities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "rating_value",
                table: "entities",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "entity_playback",
                columns: table => new
                {
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_played_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    play_count = table.Column<int>(type: "integer", nullable: false),
                    play_duration_seconds = table.Column<double>(type: "double precision", nullable: false),
                    resume_seconds = table.Column<double>(type: "double precision", nullable: false),
                    skip_count = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_playback", x => x.entity_id);
                    table.ForeignKey(
                        name: "FK_entity_playback_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entity_progress",
                columns: table => new
                {
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    index = table.Column<int>(type: "integer", nullable: false),
                    location = table.Column<string>(type: "text", nullable: true),
                    mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    total = table.Column<int>(type: "integer", nullable: false),
                    unit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_progress", x => x.entity_id);
                    table.CheckConstraint("ck_entity_progress_bounds", "index >= 0 AND total >= 0");
                    table.ForeignKey(
                        name: "FK_entity_progress_entities_current_entity_id",
                        column: x => x.current_entity_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_entity_progress_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entity_progress_current_entity_id",
                table: "entity_progress",
                column: "current_entity_id");
        }
    }
}
