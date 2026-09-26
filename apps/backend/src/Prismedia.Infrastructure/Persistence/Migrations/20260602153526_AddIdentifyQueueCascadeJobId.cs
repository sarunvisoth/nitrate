using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentifyQueueCascadeJobId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cascade_job_id",
                table: "identify_queue_items",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cascade_job_id",
                table: "identify_queue_items");
        }
    }
}
