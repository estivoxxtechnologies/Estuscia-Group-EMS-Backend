
using System.Security.Claims;
using Estuscia.Domain.Enums;
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

        // Super Admin can select a tenant through X-Tenant-Id.
        if (role == "super_admin" &&
            Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader))
        {
            if (!int.TryParse(
                    tenantHeader.ToString(),
                    out var selectedTenantId) ||
                selectedTenantId <= 0)
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

        // Only Company Admin and HR Ops may select a branch.
        if (branchId.HasValue &&
            role is not ("company_admin" or "hr_ops"))
        {
            return Forbid();
        }

        int? effectiveBranchId = role switch
        {
            "company_admin" or "hr_ops" => branchId,
            "branch_manager" => claimBranchId,
            _ => null
        };

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

        // ----------------------------------------------------
        // TENANT SCOPE
        // ----------------------------------------------------

        if (tenantId.HasValue)
        {
            var selectedTenantId = tenantId.Value;

            users = users.Where(x => x.TenantId == selectedTenantId);
            attendance = attendance.Where(x => x.TenantId == selectedTenantId);
            leaves = leaves.Where(x => x.TenantId == selectedTenantId);
            salaryChanges = salaryChanges.Where(x => x.TenantId == selectedTenantId);
            payrollCycles = payrollCycles.Where(x => x.TenantId == selectedTenantId);
            payrollRecords = payrollRecords.Where(x => x.TenantId == selectedTenantId);
            workLogs = workLogs.Where(x => x.TenantId == selectedTenantId);
            developerWorks = developerWorks.Where(x => x.TenantId == selectedTenantId);
            leadAssignments = leadAssignments.Where(x => x.TenantId == selectedTenantId);
            tenantPayments = tenantPayments.Where(x => x.TenantId == selectedTenantId);
            branches = branches.Where(x => x.TenantId == selectedTenantId);
        }
        else if (role != "super_admin")
        {
            return Forbid();
        }

        // ----------------------------------------------------
        // BRANCH SCOPE
        // ----------------------------------------------------

        if (role == "branch_manager")
        {
            if (claimBranchId is null || claimBranchId <= 0)
                return Forbid();

            effectiveBranchId = claimBranchId.Value;
        }

        if (effectiveBranchId.HasValue)
        {
            if (tenantId is null)
                return Forbid();

            var selectedBranchId = effectiveBranchId.Value;

            var validBranch = await branches.AnyAsync(
                x => x.Id == selectedBranchId,
                cancellationToken);

            if (!validBranch)
            {
                return BadRequest(new
                {
                    message = "The selected branch is invalid or is outside your tenant."
                });
            }

            users = users.Where(x => x.BranchId == selectedBranchId);
            attendance = attendance.Where(x => x.BranchId == selectedBranchId);
            leaves = leaves.Where(x => x.BranchId == selectedBranchId);
            salaryChanges = salaryChanges.Where(x => x.BranchId == selectedBranchId);
            workLogs = workLogs.Where(x => x.BranchId == selectedBranchId);
            developerWorks = developerWorks.Where(x => x.BranchId == selectedBranchId);
            leadAssignments = leadAssignments.Where(x => x.BranchId == selectedBranchId);
            branches = branches.Where(x => x.Id == selectedBranchId);

            // Payroll records are scoped through their employees.
            var branchUserIds = users.Select(x => x.Id);

            payrollRecords = payrollRecords.Where(
                x => branchUserIds.Contains(x.UserId));

            payrollCycles = payrollCycles.Where(
                x => payrollRecords.Any(r => r.PayrollCycleId == x.Id));
        }

        // ----------------------------------------------------
        // INDIVIDUAL USER SCOPE
        // ----------------------------------------------------

        var isIndividual = role is
            "sales_staff" or
            "developer" or
            "support_staff" or
            "knowledge_trainer";

        if (isIndividual)
        {
            var currentUserId = userId.Value;

            users = users.Where(x => x.Id == currentUserId);
            attendance = attendance.Where(x => x.UserId == currentUserId);
            leaves = leaves.Where(x => x.UserId == currentUserId);
            salaryChanges = salaryChanges.Where(x => x.UserId == currentUserId);
            payrollRecords = payrollRecords.Where(x => x.UserId == currentUserId);
            workLogs = workLogs.Where(x => x.UserId == currentUserId);

            // Keep ownership filtering for developer tasks and lead
            // assignments unchanged until their owner fields are confirmed.
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var monthStart = new DateTime(
            today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var nextMonth = monthStart.AddMonths(1);
        var monthStartDate = new DateOnly(today.Year, today.Month, 1);
        var nextMonthDate = monthStartDate.AddMonths(1);
        var trendStart = monthStartDate.AddMonths(-5);

        // ----------------------------------------------------
        // MAIN KPIs
        // ----------------------------------------------------

        var activeUsers = await users.CountAsync(
            x => x.IsActive,
            cancellationToken);

        var branchCount = await branches.CountAsync(
            x => x.IsActive,
            cancellationToken);

        var todayAttendance = attendance.Where(x => x.Date == today);

        var attendanceRows = await todayAttendance
            .OrderBy(x => x.User.FullName)
            .Select(x => new
            {
                x.UserId,
                employeeName = x.User.FullName,
                employeeCode = x.User.EmployeeCode,
                x.BranchId,
                x.Date,
                x.CheckInTime,
                x.CheckOutTime,
                x.WorkedHours,
                savedStatus = x.Status,

                branchStandardHours = x.Branch.StandardWorkingHours,
                branchStart = x.Branch.WorkStartTime,
                branchEnd = x.Branch.WorkEndTime,

                tenantStandardHours = x.Branch.Tenant.StandardWorkingHours,
                tenantStart = x.Branch.Tenant.WorkStartTime,
                tenantEnd = x.Branch.Tenant.WorkEndTime
            })
            .ToListAsync(cancellationToken);

        var todayAttendanceDetails = attendanceRows
            .Select(x =>
            {
                var schedule = ResolveSchedule(
                    x.branchStandardHours,
                    x.branchStart,
                    x.branchEnd,
                    x.tenantStandardHours,
                    x.tenantStart,
                    x.tenantEnd);

                var workedHours = CalculateWorkedHours(
                    x.CheckInTime,
                    x.CheckOutTime,
                    x.WorkedHours);

                var isLate = x.CheckInTime.HasValue &&
                             x.CheckInTime.Value > schedule.Start;

                var isEarlyLeaving = x.CheckOutTime.HasValue &&
                                     x.CheckOutTime.Value < schedule.End;

                var isHalfDay = x.CheckInTime.HasValue &&
                                x.CheckOutTime.HasValue &&
                                workedHours < schedule.RequiredHours / 2m;

                // IMPORTANT:
                // Do not preserve a saved Absent status if the employee
                // actually has a check-in. Recalculate using the times.
                // Approved leave remains OnLeave.
                var effectiveStatus = x.savedStatus switch
                {
                    AttendanceStatus.OnLeave =>
                        AttendanceStatus.OnLeave,

                    _ when !x.CheckInTime.HasValue =>
                        AttendanceStatus.Absent,

                    _ when isHalfDay =>
                        AttendanceStatus.HalfDay,

                    _ when isLate =>
                        AttendanceStatus.Late,

                    _ =>
                        AttendanceStatus.Present
                };

                return new
                {
                    userId = x.UserId,
                    x.employeeName,
                    x.employeeCode,
                    branchId = (int?)x.BranchId,
                    date = x.Date,
                    checkInTime = x.CheckInTime?.ToString("HH:mm"),
                    checkOutTime = x.CheckOutTime?.ToString("HH:mm"),
                    workedHours,
                    requiredHours = schedule.RequiredHours,
                    scheduledStartTime = schedule.Start.ToString("HH:mm"),
                    scheduledEndTime = schedule.End.ToString("HH:mm"),
                    status = effectiveStatus.ToString(),
                    isLate,
                    isHalfDay,
                    isEarlyLeaving
                };
            })
            .ToList();

        var attendanceTodayCount = todayAttendanceDetails.Count;

        var employeesPresentToday = todayAttendanceDetails
            .Where(x =>
                x.status == AttendanceStatus.Present.ToString() ||
                x.status == AttendanceStatus.Late.ToString() ||
                x.status == AttendanceStatus.HalfDay.ToString())
            .Select(x => x.userId)
            .Distinct()
            .Count();

        var recordedAbsencesToday = todayAttendanceDetails
            .Where(x => x.status == AttendanceStatus.Absent.ToString())
            .Select(x => x.userId)
            .Distinct()
            .Count();

        var attendanceStatus = todayAttendanceDetails
            .GroupBy(x => x.status)
            .Select(g => new
            {
                status = g.Key,
                count = g.Count()
            })
            .ToList();

        // ----------------------------------------------------
        // LEAVE REQUESTS
        // ----------------------------------------------------

        var monthlyLeaves = leaves.Where(
            x => x.CreatedAtUtc >= monthStart &&
                 x.CreatedAtUtc < nextMonth);

        var leaveRequests = await monthlyLeaves.CountAsync(
            cancellationToken);

        var leaveStatusRows = await monthlyLeaves
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key,
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var leaveStatus = leaveStatusRows
            .Select(x => new
            {
                status = x.status.ToString(),
                x.count
            })
            .ToList();

        var recentLeaveRows = await monthlyLeaves
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                employeeName = x.User.FullName,
                employeeCode = x.User.EmployeeCode,
                x.LeaveType,
                x.StartDate,
                x.EndDate,
                x.RequestedDays,
                x.Reason,
                x.Status,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var recentLeaveRequests = recentLeaveRows
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.employeeName,
                x.employeeCode,
                leaveType = x.LeaveType.ToString(),
                x.StartDate,
                x.EndDate,
                x.RequestedDays,
                x.Reason,
                status = x.Status.ToString(),
                x.CreatedAtUtc
            })
            .ToList();

        // ----------------------------------------------------
        // SALARY CHANGE STATUS
        // ----------------------------------------------------

        var salaryChangeStatusRows = await salaryChanges
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key,
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var salaryChangeStatus = salaryChangeStatusRows
            .Select(x => new
            {
                status = x.status.ToString(),
                x.count
            })
            .ToList();

        var pendingSalaryChanges = salaryChangeStatusRows
            .Where(x => x.status == SalaryChangeStatus.PendingApproval)
            .Sum(x => x.count);

        // ----------------------------------------------------
        // PAYROLL CYCLE STATUS
        // ----------------------------------------------------

        var payrollStatusRows = await payrollCycles
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key,
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var payrollStatus = payrollStatusRows
            .Select(x => new
            {
                status = x.status.ToString(),
                x.count
            })
            .ToList();

        // ----------------------------------------------------
        // MONTHLY ATTENDANCE CHART
        // Historical chart remains based on saved record statuses.
        // Today's status above is recalculated from actual time values.
        // ----------------------------------------------------

        var monthlyAttendanceGroups = await attendance
            .Where(x => x.Date >= trendStart && x.Date < nextMonthDate)
            .GroupBy(x => new
            {
                x.Date.Year,
                x.Date.Month,
                x.Status
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Status,
                records = g.Count(),
                workedHours = g.Sum(x => x.WorkedHours)
            })
            .ToListAsync(cancellationToken);

        var monthlyAttendance = monthlyAttendanceGroups
            .GroupBy(x => new { x.Year, x.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                records = g.Sum(x => x.records),
                workedHours = g.Sum(x => x.workedHours),
                present = g.Where(x => x.Status == AttendanceStatus.Present)
                    .Sum(x => x.records),
                late = g.Where(x => x.Status == AttendanceStatus.Late)
                    .Sum(x => x.records),
                absent = g.Where(x => x.Status == AttendanceStatus.Absent)
                    .Sum(x => x.records),
                halfDay = g.Where(x => x.Status == AttendanceStatus.HalfDay)
                    .Sum(x => x.records),
                onLeave = g.Where(x => x.Status == AttendanceStatus.OnLeave)
                    .Sum(x => x.records)
            })
            .ToList();

        // ----------------------------------------------------
        // PAYROLL SUMMARY
        // ----------------------------------------------------

        var currentMonthPayroll = payrollRecords.Where(
            x => x.PayrollCycle != null &&
                 x.PayrollCycle.Year == today.Year &&
                 x.PayrollCycle.Month == today.Month);

        var payrollSummaryRows = await currentMonthPayroll
            .GroupBy(x => x.PaymentStatus)
            .Select(g => new
            {
                status = g.Key,
                amount = g.Sum(x => x.NetSalary),
                employees = g.Select(x => x.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var paidAmount = payrollSummaryRows
            .Where(x => x.status == PayrollPaymentStatus.Paid)
            .Sum(x => x.amount);

        var unpaidAmount = payrollSummaryRows
            .Where(x => x.status == PayrollPaymentStatus.Unpaid)
            .Sum(x => x.amount);

        var paidEmployees = await currentMonthPayroll
            .Where(x => x.PaymentStatus == PayrollPaymentStatus.Paid)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var unpaidEmployees = await currentMonthPayroll
            .Where(x => x.PaymentStatus == PayrollPaymentStatus.Unpaid)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalPayrollEmployees = await currentMonthPayroll
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var totalNetSalary = paidAmount + unpaidAmount;

        var monthlyPayrollRows = await payrollRecords
            .Where(x =>
                x.PayrollCycle != null &&
                (x.PayrollCycle.Year * 100 + x.PayrollCycle.Month) >=
                    (trendStart.Year * 100 + trendStart.Month) &&
                (x.PayrollCycle.Year * 100 + x.PayrollCycle.Month) <
                    (nextMonthDate.Year * 100 + nextMonthDate.Month))
            .GroupBy(x => new
            {
                x.PayrollCycle!.Year,
                x.PayrollCycle.Month,
                x.PaymentStatus
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.PaymentStatus,
                amount = g.Sum(x => x.NetSalary),
                bonus = g.Sum(x => x.TotalBonus),
                deductions = g.Sum(x => x.TotalDeduction),
                employees = g.Select(x => x.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var monthlyPayroll = monthlyPayrollRows
            .GroupBy(x => new { x.Year, x.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                netSalary = g.Sum(x => x.amount),
                bonus = g.Sum(x => x.bonus),
                deductions = g.Sum(x => x.deductions),
                paidAmount = g
                    .Where(x => x.PaymentStatus == PayrollPaymentStatus.Paid)
                    .Sum(x => x.amount),
                unpaidAmount = g
                    .Where(x => x.PaymentStatus == PayrollPaymentStatus.Unpaid)
                    .Sum(x => x.amount),
                paidEmployees = g
                    .Where(x => x.PaymentStatus == PayrollPaymentStatus.Paid)
                    .Sum(x => x.employees),
                unpaidEmployees = g
                    .Where(x => x.PaymentStatus == PayrollPaymentStatus.Unpaid)
                    .Sum(x => x.employees)
            })
            .ToList();

        var payrollRecordsThisMonth = await currentMonthPayroll.CountAsync(
            cancellationToken);

        // ----------------------------------------------------
        // DEVELOPER TASKS
        // ----------------------------------------------------

        var overdueDeveloperTasks = await developerWorks.CountAsync(
            x => x.DueDateUtc < now && x.Status != 0,
            cancellationToken);

        var developerTaskStatusRows = await developerWorks
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                status = g.Key,
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var developerTaskStatus = developerTaskStatusRows
            .Select(x => new
            {
                status = x.status.ToString(),
                x.count
            })
            .ToList();

        var developerTaskPriorityRows = await developerWorks
            .GroupBy(x => x.Priority)
            .Select(g => new
            {
                priority = g.Key,
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var developerTaskPriority = developerTaskPriorityRows
            .Select(x => new
            {
                status = x.priority.ToString(),
                x.count
            })
            .ToList();

        // ----------------------------------------------------
        // MONTHLY WORK LOGS
        // ----------------------------------------------------

        var monthlyWorkLogs = await workLogs
            .Where(x => x.WorkDate >= trendStart &&
                        x.WorkDate < nextMonthDate)
            .GroupBy(x => new
            {
                x.WorkDate.Year,
                x.WorkDate.Month
            })
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

        // ----------------------------------------------------
        // SUPER ADMIN OVERVIEW
        // ----------------------------------------------------

        object? superAdminData = null;

        if (role == "super_admin")
        {
            var tenantQuery = tenants.AsNoTracking();

            if (tenantId.HasValue)
            {
                var selectedTenantId = tenantId.Value;
                tenantQuery = tenantQuery.Where(
                    x => x.Id == selectedTenantId);
            }

            var tenantCount = await tenantQuery.CountAsync(
                cancellationToken);

            var activeTenantCount = await tenantQuery.CountAsync(
                x => x.IsActive,
                cancellationToken);

            var paymentStatusRows = await tenantPayments
                .GroupBy(x => x.PaymentStatus)
                .Select(g => new
                {
                    status = g.Key,
                    count = g.Count()
                })
                .ToListAsync(cancellationToken);

            var paymentStatus = paymentStatusRows
                .Select(x => new
                {
                    status = x.status.ToString(),
                    x.count
                })
                .ToList();

            superAdminData = new
            {
                totalTenants = tenantCount,
                activeTenants = activeTenantCount,
                paymentStatus
            };
        }

        // ----------------------------------------------------
        // RESPONSE
        // ----------------------------------------------------

        return Ok(new
        {
            role,
            generatedAtUtc = now,

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
                employeesPresentToday,
                recordedAbsencesToday,
                leaveRequests,
                payrollCycles = payrollStatus.Sum(x => x.count),
                payrollRecordsThisMonth,
                pendingSalaryChanges,
                overdueDeveloperTasks
            },

            payrollSummary = new
            {
                totalNetSalary,
                paidAmount,
                unpaidAmount,
                paidEmployees,
                unpaidEmployees,
                totalEmployees = totalPayrollEmployees
            },

            todayAttendance = todayAttendanceDetails,
            recentLeaveRequests,

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

    private static decimal CalculateWorkedHours(
        TimeOnly? checkIn,
        TimeOnly? checkOut,
        decimal storedWorkedHours)
    {
        if (!checkIn.HasValue || !checkOut.HasValue)
            return storedWorkedHours;

        var start = checkIn.Value;
        var end = checkOut.Value;

        var minutes = end >= start
            ? (end - start).TotalMinutes
            : (end.AddHours(24) - start).TotalMinutes;

        return Math.Round(
            (decimal)(minutes / 60d),
            2,
            MidpointRounding.AwayFromZero);
    }

    private static (
        decimal RequiredHours,
        TimeOnly Start,
        TimeOnly End
    ) ResolveSchedule(
        decimal? branchHours,
        TimeOnly? branchStart,
        TimeOnly? branchEnd,
        decimal? tenantHours,
        TimeOnly? tenantStart,
        TimeOnly? tenantEnd)
    {
        var requiredHours =
            branchHours.GetValueOrDefault() > 0
                ? branchHours.Value
                : tenantHours.GetValueOrDefault() > 0
                    ? tenantHours.Value
                    : 8m;

        var start = branchStart
            ?? tenantStart
            ?? new TimeOnly(9, 0);

        var end = branchEnd
            ?? tenantEnd
            ?? new TimeOnly(17, 0);

        return (requiredHours, start, end);
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
