using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AstroView.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimilarHighestScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "HighestScore",
                table: "Similars",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateIndex(
                name: "IX_Similars_CaesarJobId_HighestScore",
                table: "Similars",
                columns: new[] { "CaesarJobId", "HighestScore" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Similars_CaesarJobId_HighestScore",
                table: "Similars");

            migrationBuilder.DropColumn(
                name: "HighestScore",
                table: "Similars");
        }
    }
}
