using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PrismediaDbContext))]
    [Migration("20260605161500_BackfillBookDetailClassifications")]
    public partial class BackfillBookDetailClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH child_book_details AS (
                    SELECT child.parent_entity_id AS entity_id,
                           bool_or(child_detail.book_type = 'novel') AS has_novel,
                           bool_or(child_detail.format = 'epub') AS has_epub,
                           bool_or(child_detail.format = 'pdf') AS has_pdf
                    FROM entities AS child
                    JOIN book_details AS child_detail ON child_detail.entity_id = child.id
                    WHERE child.kind_code = 'book'
                      AND child.parent_entity_id IS NOT NULL
                      AND child_detail.format IN ('epub','pdf')
                    GROUP BY child.parent_entity_id
                )
                UPDATE book_details AS parent_detail
                SET book_type = CASE WHEN child_book_details.has_novel THEN 'novel' ELSE 'book' END,
                    format = CASE WHEN child_book_details.has_epub THEN 'epub' ELSE 'pdf' END
                FROM child_book_details
                WHERE parent_detail.entity_id = child_book_details.entity_id;
                """);

            migrationBuilder.Sql("""
                UPDATE book_details AS detail
                SET book_type = 'comic',
                    format = 'image-archive'
                FROM entities AS entity
                WHERE entity.id = detail.entity_id
                  AND entity.kind_code = 'book'
                  AND entity.parent_entity_id IS NULL
                  AND detail.format = 'image-archive'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM entities AS child
                      JOIN book_details AS child_detail ON child_detail.entity_id = child.id
                      WHERE child.parent_entity_id = entity.id
                        AND child.kind_code = 'book'
                        AND child_detail.format IN ('epub','pdf')
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data classification backfill is intentionally not reversible.
        }
    }
}
