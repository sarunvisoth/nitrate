using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PrismediaDbContext))]
    [Migration("20260531000100_AddMovieEntityKind")]
    public partial class AddMovieEntityKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO entity_kinds (code, category, display_name, storage_shape)
                VALUES ('movie', 'Media', 'Movie', 'folder')
                ON CONFLICT (code) DO UPDATE
                SET category = EXCLUDED.category,
                    display_name = EXCLUDED.display_name,
                    storage_shape = EXCLUDED.storage_shape;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM entity_kinds WHERE code = 'movie';");
        }
    }
}
