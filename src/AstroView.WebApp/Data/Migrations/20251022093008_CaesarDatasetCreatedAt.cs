using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AstroView.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaesarDatasetCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CaesarDatasetCreatedAt",
                table: "DisplayModes",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaesarDatasetCreatedAt",
                table: "DisplayModes");
        }
    }
}
