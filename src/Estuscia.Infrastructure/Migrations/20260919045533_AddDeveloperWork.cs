using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estuscia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeveloperWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeveloperWorks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkType = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedToUserId = table.Column<int>(type: "int", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "int", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeveloperWorks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeveloperWorks_TenantBranches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "TenantBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeveloperWorks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeveloperWorks_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeveloperWorks_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperWorks_AssignedByUserId",
                table: "DeveloperWorks",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperWorks_AssignedToUserId",
                table: "DeveloperWorks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperWorks_BranchId",
                table: "DeveloperWorks",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperWorks_TenantId_AssignedToUserId_DueDateUtc",
                table: "DeveloperWorks",
                columns: new[] { "TenantId", "AssignedToUserId", "DueDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperWorks_TenantId_AssignedToUserId_Status",
                table: "DeveloperWorks",
                columns: new[] { "TenantId", "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperWorks_TenantId_BranchId_Status",
                table: "DeveloperWorks",
                columns: new[] { "TenantId", "BranchId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeveloperWorks");
        }
    }
}
