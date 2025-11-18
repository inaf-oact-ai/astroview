using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AstroView.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class DatasetModifiedDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "Datasets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.Sql("UPDATE Datasets SET ModifiedDate = CreatedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "Datasets");
        }
    }
}
