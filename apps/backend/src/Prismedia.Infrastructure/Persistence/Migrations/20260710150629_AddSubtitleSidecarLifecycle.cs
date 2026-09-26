using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtitleSidecarLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_entity_subtitles_entity_id_language_source",
                table: "entity_subtitles");

            migrationBuilder.AddColumn<string>(
                name: "subtitle_sidecar_signature",
                table: "video_details",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_key",
                table: "entity_subtitles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Track identity is the source stream/file, not its display language. Backfill the
            // embedded stream locator where one exists and give every legacy/manual row a stable,
            // collision-free identity. Synthetic language suffixes from the old uniqueness
            // workaround are no longer needed once duplicate languages are legal.
            migrationBuilder.Sql(
                """
                UPDATE entity_subtitles
                SET source_key = CASE
                    WHEN source = 'embedded' AND source_path ~ '^[0-9]+$'
                        THEN 'stream:' || source_path
                    ELSE 'legacy:' || replace(id::text, '-', '')
                END;

                UPDATE entity_subtitles AS subtitle
                SET language = regexp_replace(subtitle.language, '\.[0-9]+$', '')
                WHERE subtitle.source IN ('embedded', 'sidecar')
                  AND subtitle.language ~ '\.[0-9]+$'
                  AND EXISTS (
                      SELECT 1
                      FROM entity_subtitles AS base
                      WHERE base.entity_id = subtitle.entity_id
                        AND base.source = subtitle.source
                        AND base.id <> subtitle.id
                        AND base.language = regexp_replace(subtitle.language, '\.[0-9]+$', ''));

                UPDATE video_details
                SET subtitles_extracted_at = NULL;

                -- The video scan's snapshot now also contains adjacent subtitle files. Clearing
                -- only this historical scan kind forces one complete upgrade scan/backfill.
                DELETE FROM scanned_files
                WHERE scan_kind = 'scan-library';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "source_key",
                table: "entity_subtitles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_entity_subtitles_entity_id_source_source_key",
                table: "entity_subtitles",
                columns: new[] { "entity_id", "source", "source_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_entity_subtitles_entity_id_source_source_key",
                table: "entity_subtitles");

            // The legacy index allowed only one track per language/source. Make duplicate display
            // languages deterministic before recreating it so rollback remains possible.
            migrationBuilder.Sql(
                """
                WITH duplicates AS (
                    SELECT id,
                           row_number() OVER (
                               PARTITION BY entity_id, language, source
                               ORDER BY created_at, id) AS ordinal
                    FROM entity_subtitles
                )
                UPDATE entity_subtitles AS subtitle
                SET language = left(subtitle.language, 16) || '.legacy-' || left(md5(subtitle.id::text), 8)
                FROM duplicates
                WHERE subtitle.id = duplicates.id
                  AND duplicates.ordinal > 1;
                """);

            migrationBuilder.DropColumn(
                name: "subtitle_sidecar_signature",
                table: "video_details");

            migrationBuilder.DropColumn(
                name: "source_key",
                table: "entity_subtitles");

            migrationBuilder.CreateIndex(
                name: "IX_entity_subtitles_entity_id_language_source",
                table: "entity_subtitles",
                columns: new[] { "entity_id", "language", "source" },
                unique: true);
        }
    }
}
