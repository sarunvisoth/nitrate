using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookAnnotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "book_annotations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locator_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    locator = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    spine_index = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<double>(type: "double precision", nullable: false),
                    selected_text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    text_before = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    text_after = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    style = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    note = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    source_signature = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_annotations", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_annotations_entities_book_id",
                        column: x => x.book_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_annotations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_book_annotations_book_id",
                table: "book_annotations",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "IX_book_annotations_user_id_book_id",
                table: "book_annotations",
                columns: new[] { "user_id", "book_id" });

            migrationBuilder.CreateIndex(
                name: "IX_book_annotations_user_id_updated_at",
                table: "book_annotations",
                columns: new[] { "user_id", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_annotations");
        }
    }
}
