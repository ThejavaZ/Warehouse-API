using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.data.migrations
{
    /// <inheritdoc />
    public partial class AlterTableUsersRenameUUidToUUID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UUId",
                table: "Users",
                newName: "UUID");

            migrationBuilder.RenameIndex(
                name: "IX_Users_UUId",
                table: "Users",
                newName: "IX_Users_UUID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UUID",
                table: "Users",
                newName: "UUId");

            migrationBuilder.RenameIndex(
                name: "IX_Users_UUID",
                table: "Users",
                newName: "IX_Users_UUId");
        }
    }
}
