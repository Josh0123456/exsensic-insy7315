using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exsensic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingRelatedConstraintsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingStatusHistory_BookingId",
                table: "BookingStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_BookingRequirements_BookingId",
                table: "BookingRequirements");

            migrationBuilder.AlterColumn<string>(
                name: "FieldKey",
                table: "BookingRequirements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistory_BookingId_ChangedAtUtc",
                table: "BookingStatusHistory",
                columns: new[] { "BookingId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequirements_BookingId_FieldKey",
                table: "BookingRequirements",
                columns: new[] { "BookingId", "FieldKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_BookingStatusHistory_BookingId_ChangedAtUtc",
                table: "BookingStatusHistory");

            migrationBuilder.DropIndex(
                name: "IX_BookingRequirements_BookingId_FieldKey",
                table: "BookingRequirements");

            migrationBuilder.AlterColumn<string>(
                name: "FieldKey",
                table: "BookingRequirements",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistory_BookingId",
                table: "BookingStatusHistory",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequirements_BookingId",
                table: "BookingRequirements",
                column: "BookingId");
        }
    }
}
