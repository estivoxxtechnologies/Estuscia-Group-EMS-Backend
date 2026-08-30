using Estuscia.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<DailyWorkLog> DailyWorkLogs { get; }
    DbSet<CustomerReceipt> CustomerReceipts { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<InvestmentSlab> InvestmentSlabs { get; }
    DbSet<KnowledgeVideo> KnowledgeVideos { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentTenantService
{
    Guid? TenantId { get; }
    string? BranchName { get; }
    bool IsSuperAdmin { get; }
    Guid? UserId { get; }
}

public interface IJwtTokenGenerator
{
    string GenerateToken(ApplicationUser user);
    string GenerateRefreshToken();
}

public interface IExcelAttendanceParser
{
    Task<List<AttendanceRecord>> ParseAttendanceFileAsync(Stream fileStream, Guid tenantId);
}
