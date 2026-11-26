using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImageAlbumPathAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlbumPath",
                table: "Images",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Images",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_AlbumPath",
                table: "Images",
                column: "AlbumPath");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Images_AlbumPath",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "AlbumPath",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Images");
        }
    }
}
