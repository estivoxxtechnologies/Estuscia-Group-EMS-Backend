using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estuscia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditUserIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "UserModulePermissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "TenantPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "StandardWorkingHours",
                table: "TenantBranches",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "TenantBranches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "SalesLeads",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "SalesLeadAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "RoleModulePermissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Modules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "KnowledgeVideos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "InvestmentSlabs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "DeveloperWorks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "DailyWorkLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "CustomerReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "Currencies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "AttendanceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 4,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 5,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 6,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7,
                column: "UpdatedByUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 8,
                column: "UpdatedByUserId",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "UserModulePermissions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "TenantPayments");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "TenantBranches");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "SalesLeads");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "SalesLeadAssignments");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "RoleModulePermissions");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "KnowledgeVideos");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "InvestmentSlabs");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "DeveloperWorks");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "DailyWorkLogs");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CustomerReceipts");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "AttendanceRecords");

            migrationBuilder.AlterColumn<decimal>(
                name: "StandardWorkingHours",
                table: "TenantBranches",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
