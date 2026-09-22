using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estuscia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkingSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // ATTENDANCE
            // ============================================================

            migrationBuilder.RenameColumn(
                name: "OvertimeHours",
                table: "AttendanceRecords",
                newName: "WorkingHoursBalance");

            migrationBuilder.AddColumn<decimal>(
                name: "WorkedHours",
                table: "AttendanceRecords",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // ============================================================
            // TENANT WORKING SCHEDULE
            // StandardWorkingHours already exists in the database.
            // Only add the new start/end time columns.
            // ============================================================

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WorkStartTime",
                table: "Tenants",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(9, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WorkEndTime",
                table: "Tenants",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(17, 0));

            // ============================================================
            // BRANCH WORKING SCHEDULE
            // StandardWorkingHours already exists in the database.
            // NULL means inherit from Tenant.
            // ============================================================

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WorkStartTime",
                table: "TenantBranches",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "WorkEndTime",
                table: "TenantBranches",
                type: "time",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // BRANCH WORKING SCHEDULE
            // ============================================================

            migrationBuilder.DropColumn(
                name: "WorkStartTime",
                table: "TenantBranches");

            migrationBuilder.DropColumn(
                name: "WorkEndTime",
                table: "TenantBranches");

            // ============================================================
            // TENANT WORKING SCHEDULE
            // ============================================================

            migrationBuilder.DropColumn(
                name: "WorkStartTime",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "WorkEndTime",
                table: "Tenants");

            // ============================================================
            // ATTENDANCE
            // ============================================================

            migrationBuilder.DropColumn(
                name: "WorkedHours",
                table: "AttendanceRecords");

            migrationBuilder.RenameColumn(
                name: "WorkingHoursBalance",
                table: "AttendanceRecords",
                newName: "OvertimeHours");
        }
    }
}