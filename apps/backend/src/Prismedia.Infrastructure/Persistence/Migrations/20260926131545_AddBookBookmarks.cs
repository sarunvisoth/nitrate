using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookBookmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "book_bookmarks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locator_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    locator = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    spine_index = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<double>(type: "double precision", nullable: false),
                    chapter_label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_bookmarks", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_bookmarks_entities_book_id",
                        column: x => x.book_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_book_bookmarks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_book_bookmarks_book_id",
                table: "book_bookmarks",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "IX_book_bookmarks_user_id_book_id",
                table: "book_bookmarks",
                columns: new[] { "user_id", "book_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_bookmarks");
        }
    }
}
