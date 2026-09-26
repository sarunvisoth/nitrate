using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentifySearchJobStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "search_job_id",
                table: "identify_queue_items",
                type: "uuid",
                nullable: true);

            // 'search' previously meant both "never searched" and "candidates ready for choice".
            // It now means only the latter; never-searched rows become 'queued' and reconcile to an
            // error ("search again") on first read since no identify-search job owns them.
            migrationBuilder.Sql(
                """
                UPDATE identify_queue_items
                SET state = 'queued'
                WHERE state = 'search' AND candidates_json IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Old binaries cannot decode the new state codes; fold them back into 'search'.
            migrationBuilder.Sql(
                """
                UPDATE identify_queue_items
                SET state = 'search'
                WHERE state IN ('queued', 'searching');
                """);

            migrationBuilder.DropColumn(
                name: "search_job_id",
                table: "identify_queue_items");
        }
    }
}
