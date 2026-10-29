using Estuscia.Application.Common.Interfaces;
using Estuscia.Application.MonthlyAttendance;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Estuscia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class MonthlyAttendanceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _currentTenant;
    private readonly IMonthlyAttendanceService _service;

    public MonthlyAttendanceController(
        AppDbContext db,
        ICurrentTenantService currentTenant,
        IMonthlyAttendanceService service)
    {
        _db = db;
        _currentTenant = currentTenant;
        _service = service;
    }

    private string RoleName =>
        User.FindFirst(ClaimTypes.Role)?.Value
        ?? User.FindFirst("role")?.Value
        ?? string.Empty;

    private bool IsSuperAdmin =>
        RoleName.Equals("super_admin", StringComparison.OrdinalIgnoreCase);

    private bool IsCompanyAdmin =>
        RoleName.Equals("company_admin", StringComparison.OrdinalIgnoreCase);

    private bool IsHrOps =>
        RoleName.Equals("hr_ops", StringComparison.OrdinalIgnoreCase);

    private bool CanManage =>
        IsSuperAdmin || IsCompanyAdmin || IsHrOps;

    private int CurrentTenantId =>
    _currentTenant.TenantId
    ?? throw new UnauthorizedAccessException("Tenant context is missing.");

    private int CurrentUserId =>
        _currentTenant.UserId
        ?? throw new UnauthorizedAccessException("User context is missing.");

    [HttpGet]
    public async Task<IActionResult> GetReports(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] int? branchId,
        [FromQuery] int? status,
        CancellationToken cancellationToken)
    {
        if (!CanManage)
            return Forbid();

        var query = _db.MonthlyAttendanceReports
            .AsNoTracking()
            .AsQueryable();

        if (!IsSuperAdmin)
            query = query.Where(r => r.TenantId == CurrentTenantId);

        if (year.HasValue)
            query = query.Where(r => r.Year == year.Value);

        if (month.HasValue)
            query = query.Where(r => r.Month == month.Value);

        if (branchId.HasValue)
            query = query.Where(r => r.BranchId == branchId.Value);

        if (status.HasValue)
            query = query.Where(r => (int)r.Status == status.Value);

        var reports = await query
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Month)
            .Select(r => new
            {
                r.Id,
                r.TenantId,
                r.BranchId,
                BranchName = r.Branch != null
                    ? r.Branch.BranchName
                    : string.Empty,
                r.Year,
                r.Month,
                Status = r.Status.ToString(),
                r.SubmittedAtUtc,
                r.ApprovedAtUtc,
                r.ReturnReason,
                EmployeeCount = r.Employees.Count,
                EmployeesNeedingReview =
                    r.Employees.Count(e => e.NeedsHrReview)
            })
            .ToListAsync(cancellationToken);

        return Ok(reports);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetReport(
        int id,
        CancellationToken cancellationToken)
    {
        if (!CanManage)
            return Forbid();

        var query = _db.MonthlyAttendanceReports
            .AsNoTracking()
            .Include(r => r.Branch)
            .Include(r => r.Employees)
                .ThenInclude(e => e.User)
            .Where(r => r.Id == id);

        if (!IsSuperAdmin)
            query = query.Where(r => r.TenantId == CurrentTenantId);

        var report = await query.FirstOrDefaultAsync(cancellationToken);

        if (report is null)
            return NotFound();

        return Ok(new
        {
            report.Id,
            report.TenantId,
            report.BranchId,
            BranchName = report.Branch?.BranchName,
            report.Year,
            report.Month,
            Status = report.Status.ToString(),
            report.SubmittedByUserId,
            report.SubmittedAtUtc,
            report.ApprovedByUserId,
            report.ApprovedAtUtc,
            report.ReturnedByUserId,
            report.ReturnedAtUtc,
            report.ReturnReason,
            Employees = report.Employees.Select(e => new
            {
                e.Id,
                e.UserId,
                EmployeeName = e.User?.FullName,
                e.WorkingDays,
                e.PresentDays,
                e.ApprovedLeaveDays,
                e.HalfDays,
                e.AbsentDays,
                e.LateArrivals,
                e.MissingCheckIns,
                e.MissingCheckOuts,
                e.TotalWorkedHours,
                e.RequiredWorkingHours,
                e.DaysBelowRequiredHours,
                e.TotalShortageHours,
                e.MissingAttendanceDays,
                e.NeedsHrReview,
                e.HrReviewNotes
            })
        });
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateMonthlyAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManage)
            return Forbid();

        if (!IsSuperAdmin &&
            _currentTenant.BranchId.HasValue &&
            _currentTenant.BranchId.Value != request.BranchId &&
            IsHrOps)
        {
            return Forbid();
        }

        try
        {
            var result = await _service.GenerateAsync(
                request.BranchId,
                request.Year,
                request.Month,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }


    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(
        int id,
        CancellationToken cancellationToken)
    {
        if (!IsHrOps && !IsCompanyAdmin && !IsSuperAdmin)
            return Forbid();

        var report = await FindReportAsync(id, cancellationToken);

        if (report is null)
            return NotFound();

        if ((int)report.Status is not 1 and not 4)
        {
            return Conflict(new
            {
                message = "Only Draft or Returned reports can be submitted."
            });
        }

        // HR review is optional.
        // Attendance warnings remain available in the report details.

        report.Status = MonthlyAttendanceReportStatus.Submitted;
        report.SubmittedByUserId = CurrentUserId;
        report.SubmittedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Monthly attendance report submitted for approval.",
            report.Id,
            Status = report.Status.ToString()
        });
    }


    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(
        int id,
        CancellationToken cancellationToken)
    {
        if (!IsCompanyAdmin && !IsSuperAdmin)
            return Forbid();

        var report = await FindReportAsync(id, cancellationToken);

        if (report is null)
            return NotFound();

        if ((int)report.Status != 2)
            return Conflict(new
            {
                message = "Only submitted reports can be approved."
            });

        report.Status = MonthlyAttendanceReportStatus.Approved;
        report.ApprovedByUserId = CurrentUserId;
        report.ApprovedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Monthly attendance report approved and locked.",
            report.Id,
            Status = report.Status.ToString()
        });
    }

    /// <summary>
    /// Returns only finalized monthly attendance reports
    /// belonging to the currently authenticated employee.
    /// </summary>
    [HttpGet("my-reports")]
    public async Task<IActionResult> GetMyReports(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        // Staff can view their own finalized reports.
        var isStaff = RoleName.Equals(
                          "sales_staff",
                          StringComparison.OrdinalIgnoreCase)
                      || RoleName.Equals(
                          "support_staff",
                          StringComparison.OrdinalIgnoreCase)
                      || RoleName.Equals(
                          "developer",
                          StringComparison.OrdinalIgnoreCase)
                      || RoleName.Equals(
                          "knowledge_trainer",
                          StringComparison.OrdinalIgnoreCase);

        if (!isStaff)
            return Forbid();

        if (year.HasValue && (year.Value < 2000 || year.Value > 9999))
            return BadRequest(new { message = "Invalid year." });

        if (month.HasValue && (month.Value < 1 || month.Value > 12))
            return BadRequest(new { message = "Month must be between 1 and 12." });

        var currentUserId = CurrentUserId;
        var currentTenantId = CurrentTenantId;

        var query = _db.MonthlyAttendanceReports
            .AsNoTracking()
            .Where(r =>
                r.TenantId == currentTenantId &&
                r.Status == MonthlyAttendanceReportStatus.Approved &&
                r.Employees.Any(e => e.UserId == currentUserId));

        if (year.HasValue)
            query = query.Where(r => r.Year == year.Value);

        if (month.HasValue)
            query = query.Where(r => r.Month == month.Value);

        var reports = await query
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Month)
            .Select(r => new
            {
                ReportId = r.Id,
                r.Year,
                r.Month,
                Status = r.Status.ToString(),
                BranchName = r.Branch != null
                    ? r.Branch.BranchName
                    : string.Empty,
                r.ApprovedAtUtc,

                Employee = r.Employees
                    .Where(e => e.UserId == currentUserId)
                    .Select(e => new
                    {
                        e.WorkingDays,
                        e.PresentDays,
                        e.ApprovedLeaveDays,
                        e.HalfDays,
                        e.AbsentDays,
                        e.LateArrivals,
                        e.MissingCheckIns,
                        e.MissingCheckOuts,
                        e.TotalWorkedHours,
                        e.RequiredWorkingHours,
                        e.DaysBelowRequiredHours,
                        e.TotalShortageHours,
                        e.MissingAttendanceDays
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(reports
            .Where(r => r.Employee != null)
            .Select(r => new
            {
                r.ReportId,
                r.Year,
                r.Month,
                r.Status,
                r.BranchName,
                r.ApprovedAtUtc,

                r.Employee!.WorkingDays,
                r.Employee.PresentDays,
                r.Employee.ApprovedLeaveDays,
                r.Employee.HalfDays,
                r.Employee.AbsentDays,
                r.Employee.LateArrivals,
                r.Employee.MissingCheckIns,
                r.Employee.MissingCheckOuts,
                r.Employee.TotalWorkedHours,
                r.Employee.RequiredWorkingHours,
                r.Employee.DaysBelowRequiredHours,
                r.Employee.TotalShortageHours,
                r.Employee.MissingAttendanceDays
            }));
    }

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(
        int id,
        [FromBody] ReturnMonthlyAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsCompanyAdmin && !IsSuperAdmin)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new
            {
                message = "A return reason is required."
            });

        var report = await FindReportAsync(id, cancellationToken);

        if (report is null)
            return NotFound();

        if ((int)report.Status != 2)
            return Conflict(new
            {
                message = "Only submitted reports can be returned."
            });

        report.Status = report.Status = MonthlyAttendanceReportStatus.Returned;
        report.ReturnedByUserId = CurrentUserId;
        report.ReturnedAtUtc = DateTime.UtcNow;
        report.ReturnReason = request.Reason.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Report returned to HR Ops.",
            report.Id,
            Status = report.Status.ToString(),
            report.ReturnReason
        });
    }

    private async Task<MonthlyAttendanceReport?> FindReportAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var query = _db.MonthlyAttendanceReports
            .Where(r => r.Id == id);

        if (!IsSuperAdmin)
            query = query.Where(r => r.TenantId == CurrentTenantId);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }
}