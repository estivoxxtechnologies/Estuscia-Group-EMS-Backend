using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estuscia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollPaymentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollAdjustments_Users_UserId",
                table: "PayrollAdjustments");

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                table: "PayrollRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "RejectionReason",
                table: "PayrollAdjustments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "PayrollAdjustments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAdjustments_ApprovedByUserId",
                table: "PayrollAdjustments",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAdjustments_RejectedByUserId",
                table: "PayrollAdjustments",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAdjustments_TenantId_PayrollCycleId_UserId",
                table: "PayrollAdjustments",
                columns: new[] { "TenantId", "PayrollCycleId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAdjustments_TenantId_Status",
                table: "PayrollAdjustments",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollAdjustments_Tenants_TenantId",
                table: "PayrollAdjustments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollAdjustments_Users_ApprovedByUserId",
                table: "PayrollAdjustments",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollAdjustments_Users_RejectedByUserId",
                table: "PayrollAdjustments",
                column: "RejectedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollAdjustments_Users_UserId",
                table: "PayrollAdjustments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollAdjustments_Tenants_TenantId",
                table: "PayrollAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollAdjustments_Users_ApprovedByUserId",
                table: "PayrollAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollAdjustments_Users_RejectedByUserId",
                table: "PayrollAdjustments");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollAdjustments_Users_UserId",
                table: "PayrollAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_PayrollAdjustments_ApprovedByUserId",
                table: "PayrollAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_PayrollAdjustments_RejectedByUserId",
                table: "PayrollAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_PayrollAdjustments_TenantId_PayrollCycleId_UserId",
                table: "PayrollAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_PayrollAdjustments_TenantId_Status",
                table: "PayrollAdjustments");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "PayrollRecords");

            migrationBuilder.AlterColumn<string>(
                name: "RejectionReason",
                table: "PayrollAdjustments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "PayrollAdjustments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollAdjustments_Users_UserId",
                table: "PayrollAdjustments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
