using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.Application.Common.Interfaces;

public interface IAppDbContext
{
    // ============================================================
    // TENANT / USER
    // ============================================================

    DbSet<Tenant> Tenants { get; }
    DbSet<TenantBranch> TenantBranches { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<Role> Roles { get; }

    // ============================================================
    // PERMISSIONS
    // ============================================================

    DbSet<Module> Modules { get; }

    DbSet<RoleModulePermission> RoleModulePermissions { get; }

    DbSet<UserModulePermission> UserModulePermissions { get; }

    // ============================================================
    // EMS
    // ============================================================

    DbSet<DailyWorkLog> DailyWorkLogs { get; }
    DbSet<CustomerReceipt> CustomerReceipts { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<InvestmentSlab> InvestmentSlabs { get; }
    DbSet<KnowledgeVideo> KnowledgeVideos { get; }
    DbSet<AuditLog> AuditLogs { get; }

    // ============================================================
    // SAVE
    // ============================================================

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}


// ================================================================
// CURRENT TENANT / USER
// ================================================================

public interface ICurrentTenantService
{
    bool IsAuthenticated { get; }

    int? TenantId { get; }

    int? UserId { get; }

    int? BranchId { get; }

    string? BranchName { get; }

    bool IsSuperAdmin { get; }
}


// ================================================================
// JWT
// ================================================================

public interface IJwtTokenGenerator
{
    string GenerateToken(ApplicationUser user);

    string GenerateRefreshToken();
}


// ================================================================
// PERMISSIONS
// ================================================================

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(
        string moduleCode,
        PermissionAction action,
        CancellationToken cancellationToken = default);
}


// ================================================================
// EXCEL ATTENDANCE
// ================================================================

public interface IExcelAttendanceParser
{
    Task<List<AttendanceRecord>> ParseAttendanceFileAsync(
    Stream fileStream,
    int tenantId);
}