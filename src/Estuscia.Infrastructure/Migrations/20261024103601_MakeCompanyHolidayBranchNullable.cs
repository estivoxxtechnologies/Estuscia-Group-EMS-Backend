using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estuscia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCompanyHolidayBranchNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyHolidays_TenantId_BranchId_Date",
                table: "CompanyHolidays");

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "CompanyHolidays",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyHolidays_TenantId_BranchId_Date",
                table: "CompanyHolidays",
                columns: new[] { "TenantId", "BranchId", "Date" },
                unique: true,
                filter: "[TenantId] IS NOT NULL AND [BranchId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyHolidays_TenantId_BranchId_Date",
                table: "CompanyHolidays");

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "CompanyHolidays",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyHolidays_TenantId_BranchId_Date",
                table: "CompanyHolidays",
                columns: new[] { "TenantId", "BranchId", "Date" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }
    }
}
