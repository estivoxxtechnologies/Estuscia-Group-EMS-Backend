using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Estuscia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _tenantService;

    public AttendanceController(
        AppDbContext db,
        ICurrentTenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    // ============================================================
    // GET /api/Attendance
    //
    // Rules:
    //
    // super_admin
    //     -> all attendance
    //
    // company_admin / hr_ops
    //     -> entire tenant
    //     -> optional branchId
    //     -> optional userId
    //
    // branch_manager
    //     -> assigned branch only
    //
    // sales_staff
    //     -> own attendance only
    //
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetAttendance(
        [FromQuery] int? branchId,
        [FromQuery] DateOnly? date,
        [FromQuery] int? userId,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        // ========================================================
        // LOAD CURRENT USER FIRST
        //
        // This is deliberately materialized before querying
        // AttendanceRecords to avoid the open DataReader problem.
        // ========================================================

        var currentUser = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUserId)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.BranchId,
                x.RoleNumber,
                x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        // ========================================================
        // ROLE
        // ========================================================

        var isSuperAdmin = currentUser.RoleNumber == 1;
        var isCompanyAdmin = currentUser.RoleNumber == 2;
        var isHrOps = currentUser.RoleNumber == 3;
        var isBranchManager = currentUser.RoleNumber == 4;
        var isSalesStaff = currentUser.RoleNumber == 5;

        if (!isSuperAdmin &&
            !isCompanyAdmin &&
            !isHrOps &&
            !isBranchManager &&
            !isSalesStaff)
        {
            return Forbid();
        }

        // ========================================================
        // BASE QUERY
        // ========================================================

        IQueryable<AttendanceRecord> query =
            _db.AttendanceRecords.AsNoTracking();

        // ========================================================
        // TENANT SCOPE
        // ========================================================

        if (!isSuperAdmin)
        {
            if (!currentUser.TenantId.HasValue)
            {
                return BadRequest(
                    "Authenticated user is not assigned to a tenant.");
            }

            var tenantId = currentUser.TenantId.Value;

            query = query.Where(x =>
                x.TenantId == tenantId);
        }

        // ========================================================
        // SALES STAFF
        // ========================================================

        if (isSalesStaff)
        {
            if (!currentUser.BranchId.HasValue)
            {
                return BadRequest(
                    "Sales staff is not assigned to a branch.");
            }

            // Ignore userId and branchId from the client.
            query = query.Where(x =>
                x.UserId == currentUserId &&
                x.BranchId == currentUser.BranchId.Value);
        }

        // ========================================================
        // BRANCH MANAGER
        // ========================================================

        else if (isBranchManager)
        {
            if (!currentUser.BranchId.HasValue)
            {
                return BadRequest(
                    "Branch manager is not assigned to a branch.");
            }

            // Never trust branchId from client.
            query = query.Where(x =>
                x.BranchId == currentUser.BranchId.Value);
        }

        // ========================================================
        // COMPANY ADMIN / HR OPS
        // ========================================================

        else if (isCompanyAdmin || isHrOps)
        {
            if (branchId.HasValue)
            {
                query = query.Where(x =>
                    x.BranchId == branchId.Value);
            }

            if (userId.HasValue)
            {
                query = query.Where(x =>
                    x.UserId == userId.Value);
            }
        }

        // ========================================================
        // SUPER ADMIN
        // ========================================================

        else if (isSuperAdmin)
        {
            if (branchId.HasValue)
            {
                query = query.Where(x =>
                    x.BranchId == branchId.Value);
            }

            if (userId.HasValue)
            {
                query = query.Where(x =>
                    x.UserId == userId.Value);
            }
        }

        // ========================================================
        // DATE
        // ========================================================

        if (date.HasValue)
        {
            query = query.Where(x =>
                x.Date == date.Value);
        }

        // ========================================================
        // EXECUTE QUERY
        //
        // Everything is projected into a DTO-like anonymous object
        // and materialized in one SQL query.
        // ========================================================

        var records = await query
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.CheckInTime)
            .Select(x => new
            {
                x.Id,

                x.TenantId,

                x.BranchId,
                BranchName = x.Branch.BranchName,

                x.UserId,
                UserName = x.User.FullName,
                EmployeeCode = x.User.EmployeeCode,

                x.Date,

                x.CheckInTime,
                x.CheckOutTime,

                StatusId = (int)x.Status,
                Status = x.Status.ToString(),

                x.OvertimeHours,
                x.BiometricDeviceId,

                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.CreatedByUserId
            })
            .ToListAsync(cancellationToken);

        return Ok(records);
    }


    // ============================================================
    // GET /api/Attendance/{id}
    // ============================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAttendanceById(
        int id,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        // ========================================================
        // LOAD USER FIRST
        // ========================================================

        var currentUser = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUserId)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.BranchId,
                x.RoleNumber,
                x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        var isSuperAdmin = currentUser.RoleNumber == 1;
        var isCompanyAdmin = currentUser.RoleNumber == 2;
        var isHrOps = currentUser.RoleNumber == 3;
        var isBranchManager = currentUser.RoleNumber == 4;
        var isSalesStaff = currentUser.RoleNumber == 5;

        if (!isSuperAdmin &&
            !isCompanyAdmin &&
            !isHrOps &&
            !isBranchManager &&
            !isSalesStaff)
        {
            return Forbid();
        }

        // ========================================================
        // BASE QUERY
        // ========================================================

        var query = _db.AttendanceRecords
            .AsNoTracking()
            .Where(x => x.Id == id);

        // ========================================================
        // TENANT
        // ========================================================

        if (!isSuperAdmin)
        {
            if (!currentUser.TenantId.HasValue)
                return BadRequest(
                    "Authenticated user is not assigned to a tenant.");

            var tenantId = currentUser.TenantId.Value;

            query = query.Where(x =>
                x.TenantId == tenantId);
        }

        // ========================================================
        // SALES STAFF
        // ========================================================

        if (isSalesStaff)
        {
            if (!currentUser.BranchId.HasValue)
                return BadRequest(
                    "Sales staff is not assigned to a branch.");

            query = query.Where(x =>
                x.UserId == currentUserId &&
                x.BranchId == currentUser.BranchId.Value);
        }

        // ========================================================
        // BRANCH MANAGER
        // ========================================================

        if (isBranchManager)
        {
            if (!currentUser.BranchId.HasValue)
                return BadRequest(
                    "Branch manager is not assigned to a branch.");

            query = query.Where(x =>
                x.BranchId == currentUser.BranchId.Value);
        }

        // ========================================================
        // LOAD
        // ========================================================

        var record = await query
            .Select(x => new
            {
                x.Id,

                x.TenantId,

                x.BranchId,
                BranchName = x.Branch.BranchName,

                x.UserId,
                UserName = x.User.FullName,
                EmployeeCode = x.User.EmployeeCode,

                x.Date,

                x.CheckInTime,
                x.CheckOutTime,

                StatusId = (int)x.Status,
                Status = x.Status.ToString(),

                x.OvertimeHours,
                x.BiometricDeviceId,

                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.CreatedByUserId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (record == null)
            return NotFound(
                "Attendance record not found.");

        return Ok(record);
    }


    // ============================================================
    // POST /api/Attendance/check-in
    // ============================================================

    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn(
        [FromBody] CheckInRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        // ========================================================
        // LOAD USER
        // ========================================================

        var currentUser = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUserId)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.BranchId,
                x.RoleNumber,
                x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        // Super admin is not an employee attendance user.
        if (currentUser.RoleNumber == 1)
        {
            return BadRequest(
                "Super admin does not have employee attendance.");
        }

        if (!currentUser.TenantId.HasValue)
        {
            return BadRequest(
                "User is not assigned to a tenant.");
        }

        if (!currentUser.BranchId.HasValue)
        {
            return BadRequest(
                "User is not assigned to a branch.");
        }

        var tenantId = currentUser.TenantId.Value;
        var branchId = currentUser.BranchId.Value;

        // ========================================================
        // DATE / TIME
        // ========================================================

        var attendanceDate =
            request.Date ??
            DateOnly.FromDateTime(DateTime.UtcNow);

        var checkInTime =
            request.CheckInTime ??
            TimeOnly.FromDateTime(DateTime.UtcNow);

        // ========================================================
        // DUPLICATE CHECK
        // ========================================================

        var existing = await _db.AttendanceRecords
            .FirstOrDefaultAsync(
                x =>
                    x.TenantId == tenantId &&
                    x.BranchId == branchId &&
                    x.UserId == currentUserId &&
                    x.Date == attendanceDate,
                cancellationToken);

        if (existing != null)
        {
            return Conflict(new
            {
                message =
                    "Attendance already exists for this date.",

                attendanceId = existing.Id,

                checkInTime = existing.CheckInTime,

                checkOutTime = existing.CheckOutTime
            });
        }

        // ========================================================
        // BRANCH VALIDATION
        // ========================================================

        var branchExists = await _db.TenantBranches
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id == branchId &&
                    x.TenantId == tenantId &&
                    x.IsActive,
                cancellationToken);

        if (!branchExists)
        {
            return BadRequest(
                "User branch is invalid or inactive.");
        }

        // ========================================================
        // STATUS
        // ========================================================

        var status = AttendanceStatus.Present;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<AttendanceStatus>(
                    request.Status,
                    true,
                    out status))
            {
                return BadRequest(
                    $"Invalid attendance status '{request.Status}'.");
            }
        }

        // ========================================================
        // CREATE
        // ========================================================

        var attendance = new AttendanceRecord
        {
            TenantId = tenantId,
            BranchId = branchId,

            UserId = currentUserId,

            Date = attendanceDate,

            CheckInTime = checkInTime,
            CheckOutTime = null,

            Status = status,

            OvertimeHours = 0,

            BiometricDeviceId =
                request.BiometricDeviceId
        };

        _db.AttendanceRecords.Add(attendance);

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message =
                "Attendance checked in successfully.",

            attendanceId = attendance.Id,

            tenantId = attendance.TenantId,

            branchId = attendance.BranchId,

            userId = attendance.UserId,

            date = attendance.Date,

            checkInTime = attendance.CheckInTime,

            checkOutTime = attendance.CheckOutTime,

            statusId = (int)attendance.Status,

            status = attendance.Status.ToString(),

            overtimeHours = attendance.OvertimeHours
        });
    }


    // ============================================================
    // POST /api/Attendance/check-out
    // ============================================================

    [HttpPost("check-out")]
    public async Task<IActionResult> CheckOut(
        [FromBody] CheckOutRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        // ========================================================
        // LOAD USER FIRST
        // ========================================================

        var currentUser = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUserId)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.BranchId,
                x.RoleNumber,
                x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        if (currentUser.RoleNumber == 1)
        {
            return BadRequest(
                "Super admin does not have employee attendance.");
        }

        if (!currentUser.TenantId.HasValue)
        {
            return BadRequest(
                "User is not assigned to a tenant.");
        }

        if (!currentUser.BranchId.HasValue)
        {
            return BadRequest(
                "User is not assigned to a branch.");
        }

        var tenantId = currentUser.TenantId.Value;
        var branchId = currentUser.BranchId.Value;

        // ========================================================
        // DATE / TIME
        // ========================================================

        var attendanceDate =
            request.Date ??
            DateOnly.FromDateTime(DateTime.UtcNow);

        var checkOutTime =
            request.CheckOutTime ??
            TimeOnly.FromDateTime(DateTime.UtcNow);

        // ========================================================
        // FIND ATTENDANCE
        // ========================================================

        var attendance = await _db.AttendanceRecords
            .FirstOrDefaultAsync(
                x =>
                    x.TenantId == tenantId &&
                    x.BranchId == branchId &&
                    x.UserId == currentUserId &&
                    x.Date == attendanceDate,
                cancellationToken);

        if (attendance == null)
        {
            return NotFound(new
            {
                message =
                    "No attendance record found for this date."
            });
        }

        if (!attendance.CheckInTime.HasValue)
        {
            return BadRequest(
                "Attendance does not have a check-in time.");
        }

        if (attendance.CheckOutTime.HasValue)
        {
            return Conflict(new
            {
                message =
                    "Attendance has already been checked out.",

                checkOutTime =
                    attendance.CheckOutTime
            });
        }

        // ========================================================
        // VALIDATE TIME
        // ========================================================

        if (checkOutTime < attendance.CheckInTime.Value)
        {
            return BadRequest(
                "Check-out time cannot be earlier than check-in time.");
        }

        // ========================================================
        // UPDATE
        // ========================================================

        attendance.CheckOutTime = checkOutTime;

        // ========================================================
        // OVERTIME
        //
        // Current standard = 8 hours.
        // We can later make this configurable per branch/tenant.
        // ========================================================

        var workedHours =
            (checkOutTime - attendance.CheckInTime.Value)
            .TotalHours;

        attendance.OvertimeHours =
            (decimal)Math.Max(
                0,
                Math.Round(
                    workedHours - 8,
                    2));

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message =
                "Attendance checked out successfully.",

            attendanceId = attendance.Id,

            date = attendance.Date,

            checkInTime = attendance.CheckInTime,

            checkOutTime = attendance.CheckOutTime,

            overtimeHours = attendance.OvertimeHours,

            statusId = (int)attendance.Status,

            status = attendance.Status.ToString()
        });
    }


    // ============================================================
    // PUT /api/Attendance/{id}
    //
    // HR / Company Admin / Branch Manager can correct records.
    // ============================================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAttendance(
        int id,
        [FromBody] UpdateAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        // ========================================================
        // LOAD USER FIRST
        // ========================================================

        var currentUser = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUserId)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.BranchId,
                x.RoleNumber,
                x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        var isSuperAdmin = currentUser.RoleNumber == 1;
        var isCompanyAdmin = currentUser.RoleNumber == 2;
        var isHrOps = currentUser.RoleNumber == 3;
        var isBranchManager = currentUser.RoleNumber == 4;

        // Only management can modify attendance.
        if (!isSuperAdmin &&
            !isCompanyAdmin &&
            !isHrOps &&
            !isBranchManager)
        {
            return Forbid();
        }

        // ========================================================
        // FIND RECORD
        // ========================================================

        var query = _db.AttendanceRecords
            .Where(x => x.Id == id);

        // Tenant isolation.
        if (!isSuperAdmin)
        {
            if (!currentUser.TenantId.HasValue)
                return BadRequest(
                    "Authenticated user is not assigned to a tenant.");

            var tenantId = currentUser.TenantId.Value;

            query = query.Where(x =>
                x.TenantId == tenantId);
        }

        // Branch manager isolation.
        if (isBranchManager)
        {
            if (!currentUser.BranchId.HasValue)
                return BadRequest(
                    "Branch manager is not assigned to a branch.");

            query = query.Where(x =>
                x.BranchId == currentUser.BranchId.Value);
        }

        var attendance = await query
            .FirstOrDefaultAsync(cancellationToken);

        if (attendance == null)
            return NotFound(
                "Attendance record not found.");

        // ========================================================
        // UPDATE
        // ========================================================

        if (request.CheckInTime.HasValue)
        {
            attendance.CheckInTime =
                request.CheckInTime.Value;
        }

        if (request.CheckOutTime.HasValue)
        {
            attendance.CheckOutTime =
                request.CheckOutTime.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<AttendanceStatus>(
                    request.Status,
                    true,
                    out var status))
            {
                return BadRequest(
                    $"Invalid attendance status '{request.Status}'.");
            }

            attendance.Status = status;
        }

        if (request.OvertimeHours.HasValue)
        {
            if (request.OvertimeHours.Value < 0)
            {
                return BadRequest(
                    "Overtime hours cannot be negative.");
            }

            attendance.OvertimeHours =
                request.OvertimeHours.Value;
        }

        if (request.BiometricDeviceId != null)
        {
            attendance.BiometricDeviceId =
                request.BiometricDeviceId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message =
                "Attendance updated successfully.",

            attendanceId = attendance.Id
        });
    }
}


// =================================================================
// REQUEST MODELS
// =================================================================

public class CheckInRequest
{
    public DateOnly? Date { get; set; }

    public TimeOnly? CheckInTime { get; set; }

    public string? Status { get; set; }

    public string? BiometricDeviceId { get; set; }
}

public class CheckOutRequest
{
    public DateOnly? Date { get; set; }

    public TimeOnly? CheckOutTime { get; set; }
}

public class UpdateAttendanceRequest
{
    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    public string? Status { get; set; }

    public decimal? OvertimeHours { get; set; }

    public string? BiometricDeviceId { get; set; }
}