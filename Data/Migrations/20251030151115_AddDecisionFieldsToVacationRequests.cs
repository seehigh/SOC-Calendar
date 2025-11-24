using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitiowebb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionFieldsToVacationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VacationRequests_AspNetUsers_UserId",
                table: "VacationRequests");

            migrationBuilder.DropIndex(
                name: "IX_VacationRequests_UserId",
                table: "VacationRequests");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "VacationRequests",
                newName: "UserEmail");

            migrationBuilder.RenameColumn(
                name: "ManagerComment",
                table: "VacationRequests",
                newName: "DecidedUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "VacationRequests",
                newName: "CreatedUtc");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "VacationRequests");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "VacationRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DecidedByManagerId",
                table: "VacationRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecidedByManagerName",
                table: "VacationRequests",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecidedByManagerId",
                table: "VacationRequests");

            migrationBuilder.DropColumn(
                name: "DecidedByManagerName",
                table: "VacationRequests");

            migrationBuilder.RenameColumn(
                name: "UserEmail",
                table: "VacationRequests",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "DecidedUtc",
                table: "VacationRequests",
                newName: "ManagerComment");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "VacationRequests",
                newName: "CreatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "VacationRequests",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.CreateIndex(
                name: "IX_VacationRequests_UserId",
                table: "VacationRequests",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_VacationRequests_AspNetUsers_UserId",
                table: "VacationRequests",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
