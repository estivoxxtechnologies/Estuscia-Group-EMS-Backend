using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Estuscia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.Infrastructure.Services;

public sealed class MonthlyAttendanceService : IMonthlyAttendanceService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _currentTenant;

    public MonthlyAttendanceService(
        AppDbContext db,
        ICurrentTenantService currentTenant)
    {
        _db = db;
        _currentTenant = currentTenant;
    }

    public async Task<object> GenerateAsync(
        int branchId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
            throw new ArgumentException("Month must be between 1 and 12.");

        if (year is < 2000 or > 2100)
            throw new ArgumentException("Invalid report year.");

        var tenantId = _currentTenant.TenantId;

        if (tenantId <= 0)
            throw new InvalidOperationException(
                "A valid tenant context is required.");

        var branch = await _db.TenantBranches
            .FirstOrDefaultAsync(
                b => b.Id == branchId && b.TenantId == tenantId,
                cancellationToken);

        if (branch is null)
            throw new KeyNotFoundException("Branch not found.");

        var report = await _db.MonthlyAttendanceReports
            .Include(r => r.Employees)
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId
                     && r.BranchId == branchId
                     && r.Year == year
                     && r.Month == month,
                cancellationToken);

        if (report is not null &&
            (int)report.Status is not 1 and not 4)
        {
            throw new InvalidOperationException(
                "Only Draft or Returned reports can be regenerated.");
        }

        var workingDayRecords = await _db.BranchWorkingDays
            .Where(w => w.TenantId == tenantId
                        && w.BranchId == branchId)
            .ToListAsync(cancellationToken);

        if (workingDayRecords.Count != 7 ||
            workingDayRecords.Select(w => w.DayOfWeek).Distinct().Count() != 7)
        {
            throw new InvalidOperationException(
                "Configure exactly one working-day record for every weekday before generating the report.");
        }

        var workingDays = workingDayRecords
            .Where(w => w.IsWorkingDay)
            .Select(w => w.DayOfWeek)
            .ToHashSet();

        var firstDay = new DateOnly(year, month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        var holidays = await _db.CompanyHolidays
            .Where(h => h.TenantId == tenantId
                        && h.Date >= firstDay
                        && h.Date <= lastDay
                        && (h.BranchId == null || h.BranchId == branchId))
            .Select(h => h.Date)
            .ToListAsync(cancellationToken);

        var holidaySet = holidays.ToHashSet();

        var scheduledDates = Enumerable
            .Range(0, lastDay.Day)
            .Select(day => firstDay.AddDays(day))
            .Where(date => workingDays.Contains(date.DayOfWeek)
                           && !holidaySet.Contains(date))
            .ToList();

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId
                        && u.BranchId == branchId
                        && u.IsActive)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        var attendance = await _db.AttendanceRecords
            .Where(a => a.TenantId == tenantId
                        && a.BranchId == branchId
                        && userIds.Contains(a.UserId)
                        && a.Date >= firstDay
                        && a.Date <= lastDay)
            .ToListAsync(cancellationToken);

        var leaves = await _db.LeaveRequests
            .Where(l => l.TenantId == tenantId
                        && l.BranchId == branchId
                        && userIds.Contains(l.UserId)
                        && l.Status == LeaveRequestStatus.Approved
                        && l.StartDate <= lastDay
                        && l.EndDate >= firstDay)
            .ToListAsync(cancellationToken);

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(
                t => t.Id == tenantId,
                cancellationToken);

        var branchHours = branch.StandardWorkingHours;
        var requiredDailyHours = branchHours
            ?? tenant?.StandardWorkingHours
            ?? 8m;

        if (requiredDailyHours <= 0)
            throw new InvalidOperationException(
                "Standard working hours must be greater than zero.");

        var summaries = new List<MonthlyAttendanceSummary>();

        foreach (var user in users)
        {
            var employeeAttendance = attendance
                .Where(a => a.UserId == user.Id)
                .ToDictionary(a => a.Date);

            var employeeLeaves = leaves
                .Where(l => l.UserId == user.Id)
                .ToList();

            var summary = new MonthlyAttendanceSummary
            {
                TenantId = tenantId,
                BranchId = branchId,
                UserId = user.Id,
                WorkingDays = scheduledDates.Count,
                CalculatedAtUtc = DateTime.UtcNow
            };

            foreach (var date in scheduledDates)
            {
                var attendanceExists =
                    employeeAttendance.TryGetValue(date, out var record);

                var approvedLeaveExists = employeeLeaves.Any(l =>
                    date >= l.StartDate && date <= l.EndDate);

                if (attendanceExists && approvedLeaveExists)
                {
                    summary.NeedsHrReview = true;
                    AddReviewNote(
                        summary,
                        $"{date:yyyy-MM-dd}: Attendance overlaps approved leave.");

                    // Do not count the date twice.
                    // Attendance is retained for HR to resolve.
                }

                if (!attendanceExists && approvedLeaveExists)
                {
                    summary.ApprovedLeaveDays += 1m;
                    continue;
                }

                if (!attendanceExists)
                {
                    summary.MissingAttendanceDays++;
                    summary.NeedsHrReview = true;

                    AddReviewNote(
                        summary,
                        $"{date:yyyy-MM-dd}: Missing attendance and no approved leave.");

                    continue;
                }

                var workedHours = Math.Max(0m, record!.WorkedHours);
                summary.TotalWorkedHours += workedHours;
                summary.RequiredWorkingHours += requiredDailyHours;

                var status = record.Status.ToString();

                if (status.Equals(
                        "HalfDay",
                        StringComparison.OrdinalIgnoreCase))
                {
                    summary.HalfDays += 1m;
                }
                else if (status.Equals(
                             "Absent",
                             StringComparison.OrdinalIgnoreCase))
                {
                    // Do not silently classify this as a present day.
                    summary.NeedsHrReview = true;
                    AddReviewNote(
                        summary,
                        $"{date:yyyy-MM-dd}: Attendance status is Absent; verify the record.");
                }
                else
                {
                    summary.PresentDays += 1m;
                }

                if (status.Equals(
                        "Late",
                        StringComparison.OrdinalIgnoreCase))
                {
                    summary.LateArrivals++;
                }

                if (record.CheckInTime is null)
                {
                    summary.MissingCheckIns++;
                    summary.NeedsHrReview = true;
                }

                if (record.CheckOutTime is null)
                {
                    summary.MissingCheckOuts++;
                    summary.NeedsHrReview = true;
                }

                if (workedHours < requiredDailyHours)
                {
                    summary.DaysBelowRequiredHours++;
                    summary.TotalShortageHours +=
                        requiredDailyHours - workedHours;
                }
            }

            summaries.Add(summary);
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);


        if (report is null)
        {
            report = new MonthlyAttendanceReport
            {
                TenantId = tenantId,
                BranchId = branchId,
                Year = year,
                Month = month,
                Status = MonthlyAttendanceReportStatus.Draft
            };

            _db.MonthlyAttendanceReports.Add(report);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _db.MonthlyAttendanceSummaries.RemoveRange(report.Employees);
            await _db.SaveChangesAsync(cancellationToken);
        }

        foreach (var summary in summaries)
        {
            summary.ReportId = GetReportId(report);
            _db.MonthlyAttendanceSummaries.Add(summary);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new
        {
            report.Id,
            report.BranchId,
            report.Year,
            report.Month,
            Status = report.Status.ToString(),
            EmployeeCount = summaries.Count,
            EmployeesNeedingReview = summaries.Count(s => s.NeedsHrReview),
            WorkingDays = scheduledDates.Count
        };
    }

    private static int GetReportId(MonthlyAttendanceReport report)
        => report.Id;

    private static void AddReviewNote(
        MonthlyAttendanceSummary summary,
        string note)
    {
        var existing = summary.HrReviewNotes;

        if (string.IsNullOrWhiteSpace(existing))
        {
            summary.HrReviewNotes = note;
            return;
        }

        if (existing.Length < 1800)
            summary.HrReviewNotes = $"{existing}\n{note}";
    }
}