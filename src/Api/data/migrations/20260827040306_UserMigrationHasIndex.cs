using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.data.migrations
{
    /// <inheritdoc />
    public partial class UserMigrationHasIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UUId",
                table: "Users",
                column: "UUId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_UUId",
                table: "Users");
        }
    }
}
