using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReaderPreferencesAndVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_reader_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    font_family = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    font_size_percent = table.Column<int>(type: "integer", nullable: false),
                    line_height = table.Column<double>(type: "double precision", nullable: false),
                    margin_step = table.Column<int>(type: "integer", nullable: false),
                    max_content_width = table.Column<int>(type: "integer", nullable: false),
                    theme = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    flow = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    translation_target_language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_reader_preferences", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_reader_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vocabulary_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    definition = table.Column<string>(type: "text", nullable: true),
                    book_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_locator = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    source_sentence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocabulary_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_vocabulary_entries_entities_book_id",
                        column: x => x.book_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vocabulary_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vocabulary_entries_book_id",
                table: "vocabulary_entries",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "IX_vocabulary_entries_user_id_book_id",
                table: "vocabulary_entries",
                columns: new[] { "user_id", "book_id" });

            migrationBuilder.CreateIndex(
                name: "IX_vocabulary_entries_user_id_updated_at",
                table: "vocabulary_entries",
                columns: new[] { "user_id", "updated_at" });

            // Case-insensitive uniqueness per user among live (non-tombstoned) rows only: a
            // partial functional index EF's fluent API cannot express, reviewed by hand.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "UX_vocabulary_entries_user_id_word_language_live"
                ON vocabulary_entries (user_id, language, lower(word))
                WHERE deleted_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_reader_preferences");

            migrationBuilder.DropTable(
                name: "vocabulary_entries");
        }
    }
}
