using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseBackupRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                table: "database_backups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_manual",
                table: "database_backups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "size_bytes",
                table: "database_backups",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_database_backups_is_manual_expires_at",
                table: "database_backups",
                columns: new[] { "is_manual", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_database_backups_is_manual_expires_at",
                table: "database_backups");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "database_backups");

            migrationBuilder.DropColumn(
                name: "is_manual",
                table: "database_backups");

            migrationBuilder.DropColumn(
                name: "size_bytes",
                table: "database_backups");
        }
    }
}
