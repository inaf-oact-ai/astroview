using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AstroView.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class NameReversed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameReversed",
                table: "Images",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Images_DatasetId_NameReversed",
                table: "Images",
                columns: new[] { "DatasetId", "NameReversed" });

            migrationBuilder.Sql(@"UPDATE Images SET NameReversed = REVERSE(Name)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Images_DatasetId_NameReversed",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "NameReversed",
                table: "Images");
        }
    }
}
