using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estuscia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyWorkSalesUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_EmployeeCode",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserModulePermissions_TenantId_UserId_ModuleId",
                table: "UserModulePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RoleModulePermissions_TenantId_RoleNumber_ModuleId",
                table: "RoleModulePermissions");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReceipts_TenantId_ReceiptNumber",
                table: "CustomerReceipts");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "Users",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "UserModulePermissions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "RoleModulePermissions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "KnowledgeVideos",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "InvestmentSlabs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "DailyWorkLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "CustomerReceipts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "AuditLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "AttendanceRecords",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_EmployeeCode",
                table: "Users",
                columns: new[] { "TenantId", "EmployeeCode" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserModulePermissions_TenantId_UserId_ModuleId",
                table: "UserModulePermissions",
                columns: new[] { "TenantId", "UserId", "ModuleId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RoleModulePermissions_TenantId_RoleNumber_ModuleId",
                table: "RoleModulePermissions",
                columns: new[] { "TenantId", "RoleNumber", "ModuleId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailyWorkLogs_TenantId_UserId_WorkDate_WorkType",
                table: "DailyWorkLogs",
                columns: new[] { "TenantId", "UserId", "WorkDate", "WorkType" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_TenantId_ReceiptNumber",
                table: "CustomerReceipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_EmployeeCode",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserModulePermissions_TenantId_UserId_ModuleId",
                table: "UserModulePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RoleModulePermissions_TenantId_RoleNumber_ModuleId",
                table: "RoleModulePermissions");

            migrationBuilder.DropIndex(
                name: "IX_DailyWorkLogs_TenantId_UserId_WorkDate_WorkType",
                table: "DailyWorkLogs");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReceipts_TenantId_ReceiptNumber",
                table: "CustomerReceipts");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "UserModulePermissions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "RoleModulePermissions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "KnowledgeVideos",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "InvestmentSlabs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "DailyWorkLogs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "CustomerReceipts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "AuditLogs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "AttendanceRecords",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_EmployeeCode",
                table: "Users",
                columns: new[] { "TenantId", "EmployeeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserModulePermissions_TenantId_UserId_ModuleId",
                table: "UserModulePermissions",
                columns: new[] { "TenantId", "UserId", "ModuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleModulePermissions_TenantId_RoleNumber_ModuleId",
                table: "RoleModulePermissions",
                columns: new[] { "TenantId", "RoleNumber", "ModuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_TenantId_ReceiptNumber",
                table: "CustomerReceipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);
        }
    }
}
