using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estuscia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesLeadAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesLeads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesLeads_TenantBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "TenantBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLeads_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesLeadAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    SalesLeadId = table.Column<int>(type: "int", nullable: false),
                    AssignedToUserId = table.Column<int>(type: "int", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "int", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLeadAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesLeadAssignments_SalesLeads_SalesLeadId",
                        column: x => x.SalesLeadId,
                        principalTable: "SalesLeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesLeadAssignments_TenantBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "TenantBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLeadAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLeadAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLeadAssignments_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeadAssignments_AssignedByUserId",
                table: "SalesLeadAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeadAssignments_AssignedToUserId",
                table: "SalesLeadAssignments",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeadAssignments_BranchId",
                table: "SalesLeadAssignments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeadAssignments_SalesLeadId",
                table: "SalesLeadAssignments",
                column: "SalesLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeadAssignments_TenantId_AssignedToUserId_Outcome",
                table: "SalesLeadAssignments",
                columns: new[] { "TenantId", "AssignedToUserId", "Outcome" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeadAssignments_TenantId_BranchId_AssignedToUserId_AssignedAtUtc",
                table: "SalesLeadAssignments",
                columns: new[] { "TenantId", "BranchId", "AssignedToUserId", "AssignedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeads_BranchId",
                table: "SalesLeads",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeads_TenantId_PhoneNumber",
                table: "SalesLeads",
                columns: new[] { "TenantId", "PhoneNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesLeadAssignments");

            migrationBuilder.DropTable(
                name: "SalesLeads");
        }
    }
}
