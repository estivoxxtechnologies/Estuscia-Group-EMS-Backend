using Estuscia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.Infrastructure.Services;

public sealed class MonthlyAttendanceLockService
{
    private readonly AppDbContext _db;

    public MonthlyAttendanceLockService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsLockedAsync(
        int tenantId,
        int branchId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _db.MonthlyAttendanceReports
            .AnyAsync(r =>
                r.TenantId == tenantId &&
                r.BranchId == branchId &&
                r.Year == date.Year &&
                r.Month == date.Month &&
                (int)r.Status == 3,
                cancellationToken);
    }

    public async Task EnsureNotLockedAsync(
        int tenantId,
        int branchId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (await IsLockedAsync(
                tenantId,
                branchId,
                date,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Attendance is locked because the monthly report has been approved.");
        }
    }
}