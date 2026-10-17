using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Products.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ProductListIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_CreatedAtUtc_Id",
                table: "Products",
                columns: new[] { "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_CreatedAtUtc_Id",
                table: "Products");
        }
    }
}
