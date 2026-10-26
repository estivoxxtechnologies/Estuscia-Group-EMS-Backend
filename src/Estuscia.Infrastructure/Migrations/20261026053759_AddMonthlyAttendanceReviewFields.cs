using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estuscia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyAttendanceReviewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HrReviewNotes",
                table: "MonthlyAttendanceSummaries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MissingAttendanceDays",
                table: "MonthlyAttendanceSummaries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsHrReview",
                table: "MonthlyAttendanceSummaries",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HrReviewNotes",
                table: "MonthlyAttendanceSummaries");

            migrationBuilder.DropColumn(
                name: "MissingAttendanceDays",
                table: "MonthlyAttendanceSummaries");

            migrationBuilder.DropColumn(
                name: "NeedsHrReview",
                table: "MonthlyAttendanceSummaries");
        }
    }
}
