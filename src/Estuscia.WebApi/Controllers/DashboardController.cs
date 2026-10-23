
using System.Security.Claims;
using Estuscia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int? branchId,
        CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? User.FindFirstValue("RoleName");

        var userId = GetClaimInt(
            ClaimTypes.NameIdentifier, "userId", "UserId", "sub");

        var tenantId = GetClaimInt(
            "tenantId", "TenantId", "tenant_id");

        var claimBranchId = GetClaimInt(
            "branchId", "BranchId", "branch_id");

        if (string.IsNullOrWhiteSpace(role) || userId is null)
        {
            return Unauthorized(new
            {
                message = "The authenticated user or role could not be identified."
            });
        }

        role = role.Trim().ToLowerInvariant();

        var supportedRoles = new HashSet<string>
        {
            "super_admin",
            "company_admin",
            "hr_ops",
            "branch_manager",
            "sales_staff",
            "developer",
            "support_staff",
            "knowledge_trainer"
        };

        if (!supportedRoles.Contains(role))
            return Forbid();

        // Super Admin may select a tenant through X-Tenant-Id.
        // Other roles cannot override their tenant.
        if (role == "super_admin" &&
            Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader))
        {
            if (!int.TryParse(tenantHeader.ToString(), out var selectedTenantId)
                || selectedTenantId <= 0)
            {
                return BadRequest(new
                {
                    message = "The selected tenant ID is invalid."
                });
            }

            tenantId = selectedTenantId;
        }

        if (role != "super_admin" && tenantId is null)
            return Forbid();

        // Only Company Admin and HR Ops can choose a dashboard branch.
        // Branch managers always use their assigned branch.
        // Individual users remain restricted to their own records.
        int? effectiveBranchId = role switch
        {
            "company_admin" or "hr_ops" => branchId,
            "branch_manager" => claimBranchId,
            _ => null
        };

        if (branchId.HasValue &&
            role is not ("company_admin" or "hr_ops"))
        {
            return Forbid();
        }

        var users = _db.Users.AsNoTracking();
        var attendance = _db.AttendanceRecords.AsNoTracking();
        var leaves = _db.LeaveRequests.AsNoTracking();
        var salaryChanges = _db.EmployeeSalaryHistories.AsNoTracking();
        var payrollCycles = _db.PayrollCycles.AsNoTracking();
        var payrollRecords = _db.PayrollRecords.AsNoTracking();
        var workLogs = _db.DailyWorkLogs.AsNoTracking();
        var developerWorks = _db.DeveloperWorks.AsNoTracking();
        var leadAssignments = _db.SalesLeadAssignments.AsNoTracking();
        var tenantPayments = _db.TenantPayments.AsNoTracking();
        var branches = _db.TenantBranches.AsNoTracking();
        var tenants = _db.Tenants.AsNoTracking();

        // Establish tenant scope first.
        if (role != "super_admin")
        {
            users = users.Where(x => x.TenantId == tenantId);
            attendance = attendance.Where(x => x.TenantId == tenantId);
            leaves = leaves.Where(x => x.TenantId == tenantId);
            salaryChanges = salaryChanges.Where(x => x.TenantId == tenantId);
            payrollCycles = payrollCycles.Where(x => x.TenantId == tenantId);
            payrollRecords = payrollRecords.Where(x => x.TenantId == tenantId);
            workLogs = workLogs.Where(x => x.TenantId == tenantId);
            developerWorks = developerWorks.Where(x => x.TenantId == tenantId);
            leadAssignments = leadAssignments.Where(x => x.TenantId == tenantId);
            tenantPayments = tenantPayments.Where(x => x.TenantId == tenantId);
            branches = branches.Where(x => x.TenantId == tenantId);
        }
        else if (tenantId is not null)
        {
            users = users.Where(x => x.TenantId == tenantId);
            attendance = attendance.Where(x => x.TenantId == tenantId);
            leaves = leaves.Where(x => x.TenantId == tenantId);
            salaryChanges = salaryChanges.Where(x => x.TenantId == tenantId);
            payrollCycles = payrollCycles.Where(x => x.TenantId == tenantId);
            payrollRecords = payrollRecords.Where(x => x.TenantId == tenantId);
            workLogs = workLogs.Where(x => x.TenantId == tenantId);
            developerWorks = developerWorks.Where(x => x.TenantId == tenantId);
            leadAssignments = leadAssignments.Where(x => x.TenantId == tenantId);
            tenantPayments = tenantPayments.Where(x => x.TenantId == tenantId);
            branches = branches.Where(x => x.TenantId == tenantId);
        }

        // Validate the selected branch against the authenticated tenant.
        // A branch ID from another tenant must never broaden data access.
        if (effectiveBranchId.HasValue)
        {
            if (tenantId is null)
                return Forbid();

            var validBranch = await branches.AnyAsync(
                x => x.Id == effectiveBranchId.Value,
                cancellationToken);

            if (!validBranch)
            {
                return BadRequest(new
                {
                    message = "The selected branch is invalid or is outside your tenant."
                });
            }
        }

        // Apply branch scope to every branch-aware dashboard query.
        if (effectiveBranchId.HasValue)
        {
            var selected = effectiveBranchId.Value;

            users = users.Where(x => x.BranchId == selected);
            attendance = attendance.Where(x => x.BranchId == selected);
            leaves = leaves.Where(x => x.BranchId == selected);
            salaryChanges = salaryChanges.Where(x => x.BranchId == selected);
            workLogs = workLogs.Where(x => x.BranchId == selected);
            developerWorks = developerWorks.Where(x => x.BranchId == selected);
            leadAssignments = leadAssignments.Where(x => x.BranchId == selected);

            // Payroll records are scoped to users belonging to the selected
            // branch, avoiding a dependency on a PayrollRecord.BranchId column.
            var selectedBranchUserIds = users.Select(x => x.Id);
            payrollRecords = payrollRecords.Where(
                x => selectedBranchUserIds.Contains(x.UserId));
        }

        // Branch managers cannot change their scope using query parameters.
        if (role == "branch_manager")
        {
            if (claimBranchId is null)
                return Forbid();

            var selected = claimBranchId.Value;

            users = users.Where(x => x.BranchId == selected);
            attendance = attendance.Where(x => x.BranchId == selected);
            leaves = leaves.Where(x => x.BranchId == selected);
            salaryChanges = salaryChanges.Where(x => x.BranchId == selected);
            workLogs = workLogs.Where(x => x.BranchId == selected);
            developerWorks = developerWorks.Where(x => x.BranchId == selected);
            leadAssignments = leadAssignments.Where(x => x.BranchId == selected);
            branches = branches.Where(x => x.Id == selected);
        }

        // Individual users only see their own records.
        if (role is "sales_staff" or "developer"
            or "support_staff" or "knowledge_trainer")
        {
            users = users.Where(x => x.Id == userId);
            attendance = attendance.Where(x => x.UserId == userId);
            leaves = leaves.Where(x => x.UserId == userId);
            salaryChanges = salaryChanges.Where(x => x.UserId == userId);
            payrollRecords = payrollRecords.Where(x => x.UserId == userId);
            workLogs = workLogs.Where(x => x.UserId == userId);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var monthStart = new DateTime(
            today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var nextMonth = monthStart.AddMonths(1);

        var trendStart = new DateOnly(today.Year, today.Month, 1)
            .AddMonths(-5);

        var attendanceTrendEnd = DateOnly.FromDateTime(nextMonth);

        var activeUsers = await users.CountAsync(
            x => x.IsActive, cancellationToken);

        var branchCount = await branches.CountAsync(
            x => x.IsActive, cancellationToken);

        var todayAttendance = attendance.Where(x => x.Date == today);

        var attendanceTodayCount = await todayAttendance.CountAsync(
            cancellationToken);

        var attendanceStatus = await todayAttendance
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key.ToString(),
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var leaveStatus = await leaves
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key.ToString(),
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var salaryChangeStatus = await salaryChanges
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key.ToString(),
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var payrollStatus = await payrollCycles
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key.ToString(),
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var monthlyAttendance = await attendance
            .Where(x => x.Date >= trendStart &&
                        x.Date < attendanceTrendEnd)
            .GroupBy(x => new { x.Date.Year, x.Date.Month })
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                records = g.Count(),
                workedHours = g.Sum(x => x.WorkedHours)
            })
            .OrderBy(x => x.year)
            .ThenBy(x => x.month)
            .ToListAsync(cancellationToken);

        // Payroll cycles are currently tenant-scoped. If PayrollCycle has no
        // branch relationship, its totals cannot be accurately split by branch.
        var monthlyPayroll = await payrollCycles
            .Where(x =>
                x.Year * 100 + x.Month >= trendStart.Year * 100 + trendStart.Month &&
                x.Year * 100 + x.Month < nextMonth.Year * 100 + nextMonth.Month)
            .GroupBy(x => new { x.Year, x.Month })
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                netSalary = g.Sum(x => x.TotalNetSalary),
                bonus = g.Sum(x => x.TotalBonus),
                deductions = g.Sum(x => x.TotalDeduction)
            })
            .OrderBy(x => x.year)
            .ThenBy(x => x.month)
            .ToListAsync(cancellationToken);

        var monthPayrollRecords = payrollRecords.Where(
            x => x.CreatedAtUtc >= monthStart &&
                 x.CreatedAtUtc < nextMonth);

        var pendingSalaryChanges = await salaryChanges.CountAsync(
            x => x.Status == 0, cancellationToken);

        var overdueDeveloperTasks = await developerWorks.CountAsync(
            x => x.DueDateUtc < DateTime.UtcNow && x.Status != 0,
            cancellationToken);

        var developerTaskStatus = await developerWorks
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key.ToString(),
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var developerTaskPriority = await developerWorks
            .GroupBy(x => x.Priority)
            .Select(g => new
            {
                priority = g.Key.ToString(),
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var monthlyWorkLogs = await workLogs
            .Where(x => x.WorkDate >= trendStart &&
                        x.WorkDate < attendanceTrendEnd)
            .GroupBy(x => new { x.WorkDate.Year, x.WorkDate.Month })
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                entries = g.Count(),
                hours = g.Sum(x => x.HoursSpent)
            })
            .OrderBy(x => x.year)
            .ThenBy(x => x.month)
            .ToListAsync(cancellationToken);

        object? superAdminData = null;

        if (role == "super_admin")
        {
            var tenantQuery = tenants.AsNoTracking();

            if (tenantId is not null)
                tenantQuery = tenantQuery.Where(x => x.Id == tenantId);

            var tenantCount = await tenantQuery.CountAsync(cancellationToken);

            var activeTenantCount = await tenantQuery.CountAsync(
                x => x.IsActive, cancellationToken);

            var paymentStatus = await tenantPayments
                .GroupBy(x => x.PaymentStatus)
                .Select(g => new
                {
                    status = g.Key,
                    count = g.Count()
                })
                .ToListAsync(cancellationToken);

            superAdminData = new
            {
                totalTenants = tenantCount,
                activeTenants = activeTenantCount,
                paymentStatus
            };
        }

        return Ok(new
        {
            role,
            generatedAtUtc = DateTime.UtcNow,
            period = new
            {
                today,
                monthStart,
                nextMonth
            },
            kpis = new
            {
                activeUsers,
                activeBranches = branchCount,
                attendanceRecordsToday = attendanceTodayCount,
                leaveRequests = leaveStatus.Sum(x => x.count),
                payrollCycles = payrollStatus.Sum(x => x.count),
                payrollRecordsThisMonth = await monthPayrollRecords.CountAsync(
                    cancellationToken),
                pendingSalaryChanges,
                overdueDeveloperTasks
            },
            charts = new
            {
                attendanceStatus,
                leaveStatus,
                salaryChangeStatus,
                payrollStatus,
                monthlyAttendance,
                monthlyPayroll,
                developerTaskStatus,
                developerTaskPriority,
                monthlyWorkLogs
            },
            superAdmin = superAdminData
        });
    }

    private int? GetClaimInt(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = User.FindFirstValue(claimType);

            if (int.TryParse(value, out var parsed))
                return parsed;
        }

        return null;
    }
}
