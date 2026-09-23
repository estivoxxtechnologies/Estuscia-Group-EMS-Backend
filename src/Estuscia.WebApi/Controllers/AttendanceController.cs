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

    // ============================================================
    // ATTENDANCE GRACE PERIOD
    //
    // 10 minutes is allowed for:
    //
    // 1. Late coming
    // 2. Early leaving
    //
    // This is ONLY a status threshold.
    //
    // It does NOT modify:
    // - CheckInTime
    // - CheckOutTime
    // - WorkedHours
    // - OvertimeHours
    // - WorkingHoursBalance
    //
    // Examples:
    //
    // Start = 09:00
    // 09:10 -> NOT Late
    // 09:11 -> Late
    //
    // End = 17:00
    // 16:50 -> NOT Early
    // 16:49 -> Early
    // ============================================================

    private const int GracePeriodMinutes = 10;

    public AttendanceController(
        AppDbContext db,
        ICurrentTenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    // ============================================================
    // SYSTEM DATE / TIME
    //
    // Attendance uses the backend server's system local time.
    //
    // IMPORTANT:
    // Do NOT use DateTime.UtcNow for attendance date/time.
    //
    // DateTime.UtcNow is still used for CreatedAtUtc/UpdatedAtUtc
    // because those database audit fields are UTC fields.
    // ============================================================

    private static DateTime SystemNow()
    {
        return DateTime.Now;
    }

    private static DateOnly SystemDate()
    {
        return DateOnly.FromDateTime(SystemNow());
    }

    private static TimeOnly SystemTime()
    {
        return TimeOnly.FromDateTime(SystemNow());
    }

    // ============================================================
    // GET /api/Attendance
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetAttendance(
        [FromQuery] int? branchId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int? userId,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId =
            _tenantService.UserId.Value;

        // ========================================================
        // DATE RANGE VALIDATION
        // ========================================================

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value > toDate.Value)
        {
            return BadRequest(
                "fromDate cannot be later than toDate.");
        }

        // ========================================================
        // LOAD CURRENT USER FIRST
        //
        // Important for avoiding the previous DataReader issue.
        // ========================================================

        var currentUser =
            await _db.Users
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
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        // ========================================================
        // ROLE
        // ========================================================

        var isSuperAdmin =
            currentUser.RoleNumber == 1;

        var isCompanyAdmin =
            currentUser.RoleNumber == 2;

        var isHrOps =
            currentUser.RoleNumber == 3;

        var isBranchManager =
            currentUser.RoleNumber == 4;

        var isSalesStaff =
            currentUser.RoleNumber == 5;

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
            _db.AttendanceRecords
                .AsNoTracking();

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

            var tenantId =
                currentUser.TenantId.Value;

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

            query = query.Where(x =>
                x.BranchId == currentUser.BranchId.Value);

            // Branch manager cannot use another branch filter.
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
        // DATE RANGE
        // ========================================================

        if (fromDate.HasValue)
        {
            query = query.Where(x =>
                x.Date >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x =>
                x.Date <= toDate.Value);
        }

        // ========================================================
        // LOAD RAW DATA
        //
        // Materialize first.
        // Calculations happen AFTER ToListAsync.
        // ========================================================

        var rawRecords =
            await query
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.CheckInTime)
                .Select(x => new
                {
                    x.Id,

                    x.TenantId,

                    x.BranchId,

                    BranchName =
                        x.Branch.BranchName,

                    // =================================================
                    // BRANCH SCHEDULE
                    // =================================================

                    BranchStandardWorkingHours =
                        x.Branch.StandardWorkingHours,

                    BranchWorkStartTime =
                        x.Branch.WorkStartTime,

                    BranchWorkEndTime =
                        x.Branch.WorkEndTime,

                    // =================================================
                    // TENANT SCHEDULE
                    // =================================================

                    TenantStandardWorkingHours =
                        x.Branch.Tenant.StandardWorkingHours,

                    TenantWorkStartTime =
                        x.Branch.Tenant.WorkStartTime,

                    TenantWorkEndTime =
                        x.Branch.Tenant.WorkEndTime,

                    // =================================================
                    // USER
                    // =================================================

                    x.UserId,

                    UserName =
                        x.User.FullName,

                    EmployeeCode =
                        x.User.EmployeeCode,

                    // =================================================
                    // ATTENDANCE
                    // =================================================

                    x.Date,

                    x.CheckInTime,

                    x.CheckOutTime,

                    StatusId =
                        (int)x.Status,

                    Status =
                        x.Status.ToString(),

                    x.WorkedHours,

                    x.WorkingHoursBalance,

                    x.BiometricDeviceId,

                    x.CreatedAtUtc,

                    x.UpdatedAtUtc,

                    x.CreatedByUserId
                })
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // CALCULATE AFTER MATERIALIZATION
        // ========================================================

        var records =
            rawRecords
                .Select(x =>
                {
                    var schedule =
                        ResolveSchedule(
                            x.BranchStandardWorkingHours,
                            x.BranchWorkStartTime,
                            x.BranchWorkEndTime,
                            x.TenantStandardWorkingHours,
                            x.TenantWorkStartTime,
                            x.TenantWorkEndTime);

                    var calculation =
                        CalculateAttendance(
                            x.CheckInTime,
                            x.CheckOutTime,
                            schedule.StandardWorkingHours,
                            schedule.WorkStartTime,
                            schedule.WorkEndTime);

                    return new
                    {
                        x.Id,

                        x.TenantId,

                        x.BranchId,
                        x.BranchName,

                        x.UserId,
                        x.UserName,
                        x.EmployeeCode,

                        x.Date,

                        x.CheckInTime,
                        x.CheckOutTime,

                        // =================================================
                        // EFFECTIVE SCHEDULE
                        // =================================================

                        RequiredHours =
                            schedule.StandardWorkingHours,

                        ScheduledStartTime =
                            schedule.WorkStartTime,

                        ScheduledEndTime =
                            schedule.WorkEndTime,

                        ScheduleSource =
                            schedule.Source,

                        // =================================================
                        // CALCULATED VALUES
                        // =================================================

                        WorkedHours =
                            calculation.WorkedHours,

                        OvertimeHours =
                            calculation.OvertimeHours,

                        WorkingHoursBalance =
                            calculation.WorkingHoursBalance,

                        // =================================================
                        // STATUS FLAGS
                        // =================================================

                        IsLate =
                            calculation.IsLate,

                        IsEarlyLeaving =
                            calculation.IsEarlyLeaving,

                        IsHalfDay =
                            calculation.IsHalfDay,

                        // =================================================
                        // STATUS
                        // =================================================
                        //
                        // IMPORTANT:
                        // Return the calculated status, not the old
                        // database status.
                        //
                        // This means existing attendance records also
                        // immediately reflect the HalfDay rule.
                        // =================================================

                        StatusId =
                            (int)calculation.Status,

                        Status =
                            calculation.Status.ToString(),

                        x.BiometricDeviceId,

                        x.CreatedAtUtc,
                        x.UpdatedAtUtc,
                        x.CreatedByUserId
                    };
                })
                .ToList();

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

        var currentUserId =
            _tenantService.UserId.Value;

        // ========================================================
        // LOAD CURRENT USER FIRST
        // ========================================================

        var currentUser =
            await _db.Users
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
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        // ========================================================
        // ROLE
        // ========================================================

        var isSuperAdmin =
            currentUser.RoleNumber == 1;

        var isCompanyAdmin =
            currentUser.RoleNumber == 2;

        var isHrOps =
            currentUser.RoleNumber == 3;

        var isBranchManager =
            currentUser.RoleNumber == 4;

        var isSalesStaff =
            currentUser.RoleNumber == 5;

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

        var query =
            _db.AttendanceRecords
                .AsNoTracking()
                .Where(x => x.Id == id);

        // ========================================================
        // TENANT
        // ========================================================

        if (!isSuperAdmin)
        {
            if (!currentUser.TenantId.HasValue)
            {
                return BadRequest(
                    "Authenticated user is not assigned to a tenant.");
            }

            var tenantId =
                currentUser.TenantId.Value;

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

            query = query.Where(x =>
                x.BranchId == currentUser.BranchId.Value);
        }

        // ========================================================
        // LOAD RECORD
        // ========================================================

        var raw =
            await query
                .Select(x => new
                {
                    x.Id,

                    x.TenantId,

                    x.BranchId,

                    BranchName =
                        x.Branch.BranchName,

                    // =================================================
                    // BRANCH SCHEDULE
                    // =================================================

                    BranchStandardWorkingHours =
                        x.Branch.StandardWorkingHours,

                    BranchWorkStartTime =
                        x.Branch.WorkStartTime,

                    BranchWorkEndTime =
                        x.Branch.WorkEndTime,

                    // =================================================
                    // TENANT SCHEDULE
                    // =================================================

                    TenantStandardWorkingHours =
                        x.Branch.Tenant.StandardWorkingHours,

                    TenantWorkStartTime =
                        x.Branch.Tenant.WorkStartTime,

                    TenantWorkEndTime =
                        x.Branch.Tenant.WorkEndTime,

                    // =================================================
                    // USER
                    // =================================================

                    x.UserId,

                    UserName =
                        x.User.FullName,

                    EmployeeCode =
                        x.User.EmployeeCode,

                    // =================================================
                    // ATTENDANCE
                    // =================================================

                    x.Date,

                    x.CheckInTime,

                    x.CheckOutTime,

                    x.BiometricDeviceId,

                    x.CreatedAtUtc,

                    x.UpdatedAtUtc,

                    x.CreatedByUserId
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (raw == null)
        {
            return NotFound(
                "Attendance record not found.");
        }

        // ========================================================
        // RESOLVE SCHEDULE
        // ========================================================

        var schedule =
            ResolveSchedule(
                raw.BranchStandardWorkingHours,
                raw.BranchWorkStartTime,
                raw.BranchWorkEndTime,
                raw.TenantStandardWorkingHours,
                raw.TenantWorkStartTime,
                raw.TenantWorkEndTime);

        // ========================================================
        // CALCULATE
        // ========================================================

        var calculation =
            CalculateAttendance(
                raw.CheckInTime,
                raw.CheckOutTime,
                schedule.StandardWorkingHours,
                schedule.WorkStartTime,
                schedule.WorkEndTime);

        return Ok(new
        {
            raw.Id,

            raw.TenantId,

            raw.BranchId,
            raw.BranchName,

            raw.UserId,
            raw.UserName,
            raw.EmployeeCode,

            raw.Date,

            raw.CheckInTime,
            raw.CheckOutTime,

            // =====================================================
            // SCHEDULE
            // =====================================================

            requiredHours =
                schedule.StandardWorkingHours,

            scheduledStartTime =
                schedule.WorkStartTime,

            scheduledEndTime =
                schedule.WorkEndTime,

            scheduleSource =
                schedule.Source,

            // =====================================================
            // CALCULATED
            // =====================================================

            workedHours =
                calculation.WorkedHours,

            overtimeHours =
                calculation.OvertimeHours,

            workingHoursBalance =
                calculation.WorkingHoursBalance,

            // =====================================================
            // STATUS FLAGS
            // =====================================================

            isLate =
                calculation.IsLate,

            isEarlyLeaving =
                calculation.IsEarlyLeaving,

            isHalfDay =
                calculation.IsHalfDay,

            // =====================================================
            // STATUS
            // =====================================================

            statusId =
                (int)calculation.Status,

            status =
                calculation.Status.ToString(),

            raw.BiometricDeviceId,

            raw.CreatedAtUtc,
            raw.UpdatedAtUtc,
            raw.CreatedByUserId
        });
    }

    // ============================================================
    // POST /api/Attendance/check-in
    //
    // BACKEND SYSTEM TIME
    //
    // The frontend does NOT control the actual check-in time.
    // ============================================================

    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn(
        [FromBody] CheckInRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId =
            _tenantService.UserId.Value;

        // ========================================================
        // LOAD USER
        // ========================================================

        var currentUser =
            await _db.Users
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
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        // ========================================================
        // SUPER ADMIN
        // ========================================================

        if (currentUser.RoleNumber == 1)
        {
            return BadRequest(
                "Super admin does not have employee attendance.");
        }

        // ========================================================
        // TENANT
        // ========================================================

        if (!currentUser.TenantId.HasValue)
        {
            return BadRequest(
                "User is not assigned to a tenant.");
        }

        // ========================================================
        // BRANCH
        // ========================================================

        if (!currentUser.BranchId.HasValue)
        {
            return BadRequest(
                "User is not assigned to a branch.");
        }

        var tenantId =
            currentUser.TenantId.Value;

        var branchId =
            currentUser.BranchId.Value;

        // ========================================================
        // SYSTEM DATE / TIME
        //
        // IMPORTANT:
        //
        // Client Date / CheckInTime are intentionally ignored.
        //
        // Backend determines both values.
        // ========================================================

        var now =
            SystemNow();

        var attendanceDate =
            DateOnly.FromDateTime(now);

        var checkInTime =
            TimeOnly.FromDateTime(now);

        // ========================================================
        // DUPLICATE CHECK
        // ========================================================

        var existing =
            await _db.AttendanceRecords
                .AsNoTracking()
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

                attendanceId =
                    existing.Id,

                checkInTime =
                    existing.CheckInTime,

                checkOutTime =
                    existing.CheckOutTime
            });
        }

        // ========================================================
        // LOAD BRANCH + TENANT SCHEDULE
        // ========================================================

        var scheduleData =
            await _db.TenantBranches
                .AsNoTracking()
                .Where(x =>
                    x.Id == branchId &&
                    x.TenantId == tenantId &&
                    x.IsActive)
                .Select(x => new
                {
                    BranchStandardWorkingHours =
                        x.StandardWorkingHours,

                    BranchWorkStartTime =
                        x.WorkStartTime,

                    BranchWorkEndTime =
                        x.WorkEndTime,

                    TenantStandardWorkingHours =
                        x.Tenant.StandardWorkingHours,

                    TenantWorkStartTime =
                        x.Tenant.WorkStartTime,

                    TenantWorkEndTime =
                        x.Tenant.WorkEndTime
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (scheduleData == null)
        {
            return BadRequest(
                "User branch is invalid or inactive.");
        }

        var schedule =
            ResolveSchedule(
                scheduleData.BranchStandardWorkingHours,
                scheduleData.BranchWorkStartTime,
                scheduleData.BranchWorkEndTime,
                scheduleData.TenantStandardWorkingHours,
                scheduleData.TenantWorkStartTime,
                scheduleData.TenantWorkEndTime);

        // ========================================================
        // DETERMINE LATE STATUS
        //
        // 09:00 start:
        //
        // 09:10 -> NOT Late
        // 09:11 -> Late
        // ========================================================

        var isLate =
            IsLate(
                checkInTime,
                schedule.WorkStartTime);

        // ========================================================
        // STATUS
        //
        // HalfDay cannot be determined at check-in because
        // checkout has not happened yet.
        //
        // Therefore:
        //
        // Late -> Late
        // Otherwise -> Present
        // ========================================================

        var status =
            isLate
                ? AttendanceStatus.Late
                : AttendanceStatus.Present;

        // ========================================================
        // CREATE
        // ========================================================

        var attendance =
            new AttendanceRecord
            {
                TenantId =
                    tenantId,

                BranchId =
                    branchId,

                UserId =
                    currentUserId,

                Date =
                    attendanceDate,

                CheckInTime =
                    checkInTime,

                CheckOutTime =
                    null,

                Status =
                    status,

                WorkedHours =
                    0m,

                WorkingHoursBalance =
                    0m,

                BiometricDeviceId =
                    request.BiometricDeviceId
            };

        _db.AttendanceRecords.Add(
            attendance);

        await _db.SaveChangesAsync(
            cancellationToken);

        // ========================================================
        // CALCULATE CURRENT STATE
        // ========================================================

        var calculation =
            CalculateAttendance(
                attendance.CheckInTime,
                attendance.CheckOutTime,
                schedule.StandardWorkingHours,
                schedule.WorkStartTime,
                schedule.WorkEndTime);

        return Ok(new
        {
            message =
                "Attendance checked in successfully.",

            attendanceId =
                attendance.Id,

            tenantId =
                attendance.TenantId,

            branchId =
                attendance.BranchId,

            userId =
                attendance.UserId,

            date =
                attendance.Date,

            checkInTime =
                attendance.CheckInTime,

            checkOutTime =
                attendance.CheckOutTime,

            // =====================================================
            // SCHEDULE
            // =====================================================

            requiredHours =
                schedule.StandardWorkingHours,

            scheduledStartTime =
                schedule.WorkStartTime,

            scheduledEndTime =
                schedule.WorkEndTime,

            scheduleSource =
                schedule.Source,

            // =====================================================
            // CALCULATED
            // =====================================================

            workedHours =
                calculation.WorkedHours,

            overtimeHours =
                calculation.OvertimeHours,

            workingHoursBalance =
                calculation.WorkingHoursBalance,

            // =====================================================
            // STATUS FLAGS
            // =====================================================

            isLate =
                calculation.IsLate,

            isEarlyLeaving =
                calculation.IsEarlyLeaving,

            isHalfDay =
                calculation.IsHalfDay,

            // =====================================================
            // STATUS
            // =====================================================

            statusId =
                (int)calculation.Status,

            status =
                calculation.Status.ToString(),

            biometricDeviceId =
                attendance.BiometricDeviceId
        });
    }

    // ============================================================
    // POST /api/Attendance/check-out
    //
    // BACKEND SYSTEM TIME
    // ============================================================

    [HttpPost("check-out")]
    public async Task<IActionResult> CheckOut(
        [FromBody] CheckOutRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId =
            _tenantService.UserId.Value;

        // ========================================================
        // LOAD USER
        // ========================================================

        var currentUser =
            await _db.Users
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
                .FirstOrDefaultAsync(
                    cancellationToken);

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

        var tenantId =
            currentUser.TenantId.Value;

        var branchId =
            currentUser.BranchId.Value;

        // ========================================================
        // SYSTEM DATE / TIME
        //
        // Client Date / CheckOutTime are intentionally ignored.
        // ========================================================

        var now =
            SystemNow();

        var attendanceDate =
            DateOnly.FromDateTime(now);

        var checkOutTime =
            TimeOnly.FromDateTime(now);

        // ========================================================
        // FIND ATTENDANCE
        // ========================================================

        var attendance =
            await _db.AttendanceRecords
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
                    "No attendance record found for today."
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

        if (checkOutTime <
            attendance.CheckInTime.Value)
        {
            return BadRequest(
                "Check-out time cannot be earlier than check-in time.");
        }

        // ========================================================
        // LOAD SCHEDULE
        // ========================================================

        var scheduleData =
            await _db.TenantBranches
                .AsNoTracking()
                .Where(x =>
                    x.Id == branchId &&
                    x.TenantId == tenantId &&
                    x.IsActive)
                .Select(x => new
                {
                    BranchStandardWorkingHours =
                        x.StandardWorkingHours,

                    BranchWorkStartTime =
                        x.WorkStartTime,

                    BranchWorkEndTime =
                        x.WorkEndTime,

                    TenantStandardWorkingHours =
                        x.Tenant.StandardWorkingHours,

                    TenantWorkStartTime =
                        x.Tenant.WorkStartTime,

                    TenantWorkEndTime =
                        x.Tenant.WorkEndTime
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (scheduleData == null)
        {
            return BadRequest(
                "User branch is invalid or inactive.");
        }

        var schedule =
            ResolveSchedule(
                scheduleData.BranchStandardWorkingHours,
                scheduleData.BranchWorkStartTime,
                scheduleData.BranchWorkEndTime,
                scheduleData.TenantStandardWorkingHours,
                scheduleData.TenantWorkStartTime,
                scheduleData.TenantWorkEndTime);

        // ========================================================
        // SET CHECK-OUT
        // ========================================================

        attendance.CheckOutTime =
            checkOutTime;

        // ========================================================
        // CALCULATE EVERYTHING
        // ========================================================

        var calculation =
            CalculateAttendance(
                attendance.CheckInTime,
                attendance.CheckOutTime,
                schedule.StandardWorkingHours,
                schedule.WorkStartTime,
                schedule.WorkEndTime);

        attendance.WorkedHours =
            calculation.WorkedHours;

        attendance.WorkingHoursBalance =
            calculation.WorkingHoursBalance;

        // ========================================================
        // STATUS
        //
        // IMPORTANT:
        //
        // HalfDay has priority over Late.
        //
        // Example:
        //
        // Required = 8
        // Worked = 3.5
        //
        // HalfDay = true
        //
        // Even if employee was late, final status is HalfDay.
        // ========================================================

        attendance.Status =
            calculation.Status;

        await _db.SaveChangesAsync(
            cancellationToken);

        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            message =
                "Attendance checked out successfully.",

            attendanceId =
                attendance.Id,

            tenantId =
                attendance.TenantId,

            branchId =
                attendance.BranchId,

            userId =
                attendance.UserId,

            date =
                attendance.Date,

            checkInTime =
                attendance.CheckInTime,

            checkOutTime =
                attendance.CheckOutTime,

            // =====================================================
            // SCHEDULE
            // =====================================================

            requiredHours =
                schedule.StandardWorkingHours,

            scheduledStartTime =
                schedule.WorkStartTime,

            scheduledEndTime =
                schedule.WorkEndTime,

            scheduleSource =
                schedule.Source,

            // =====================================================
            // CALCULATED
            // =====================================================

            workedHours =
                calculation.WorkedHours,

            overtimeHours =
                calculation.OvertimeHours,

            workingHoursBalance =
                calculation.WorkingHoursBalance,

            // =====================================================
            // STATUS FLAGS
            // =====================================================

            isLate =
                calculation.IsLate,

            isEarlyLeaving =
                calculation.IsEarlyLeaving,

            isHalfDay =
                calculation.IsHalfDay,

            // =====================================================
            // STATUS
            // =====================================================

            statusId =
                (int)attendance.Status,

            status =
                attendance.Status.ToString(),

            biometricDeviceId =
                attendance.BiometricDeviceId
        });
    }

    // ============================================================
    // PUT /api/Attendance/{id}
    //
    // Management can correct attendance.
    //
    // Overtime is NEVER accepted from frontend.
    // It is always recalculated by backend.
    // ============================================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAttendance(
        int id,
        [FromBody] UpdateAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId =
            _tenantService.UserId.Value;

        // ========================================================
        // LOAD CURRENT USER FIRST
        // ========================================================

        var currentUser =
            await _db.Users
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
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        // ========================================================
        // ROLE
        // ========================================================

        var isSuperAdmin =
            currentUser.RoleNumber == 1;

        var isCompanyAdmin =
            currentUser.RoleNumber == 2;

        var isHrOps =
            currentUser.RoleNumber == 3;

        var isBranchManager =
            currentUser.RoleNumber == 4;

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

        var query =
            _db.AttendanceRecords
                .Where(x => x.Id == id);

        // ========================================================
        // TENANT
        // ========================================================

        if (!isSuperAdmin)
        {
            if (!currentUser.TenantId.HasValue)
            {
                return BadRequest(
                    "Authenticated user is not assigned to a tenant.");
            }

            var tenantId =
                currentUser.TenantId.Value;

            query = query.Where(x =>
                x.TenantId == tenantId);
        }

        // ========================================================
        // BRANCH MANAGER
        // ========================================================

        if (isBranchManager)
        {
            if (!currentUser.BranchId.HasValue)
            {
                return BadRequest(
                    "Branch manager is not assigned to a branch.");
            }

            query = query.Where(x =>
                x.BranchId ==
                currentUser.BranchId.Value);
        }

        // ========================================================
        // LOAD ATTENDANCE
        // ========================================================

        var attendance =
            await query.FirstOrDefaultAsync(
                cancellationToken);

        if (attendance == null)
        {
            return NotFound(
                "Attendance record not found.");
        }

        // ========================================================
        // UPDATE CHECK-IN
        // ========================================================

        if (request.CheckInTime.HasValue)
        {
            attendance.CheckInTime =
                request.CheckInTime.Value;
        }

        // ========================================================
        // UPDATE CHECK-OUT
        // ========================================================

        if (request.CheckOutTime.HasValue)
        {
            attendance.CheckOutTime =
                request.CheckOutTime.Value;
        }

        // ========================================================
        // VALIDATE TIME
        // ========================================================

        if (attendance.CheckInTime.HasValue &&
            attendance.CheckOutTime.HasValue)
        {
            if (attendance.CheckOutTime.Value <
                attendance.CheckInTime.Value)
            {
                return BadRequest(
                    "Check-out time cannot be earlier than check-in time.");
            }
        }

        // ========================================================
        // LOAD SCHEDULE
        // ========================================================

        var scheduleData =
            await _db.TenantBranches
                .AsNoTracking()
                .Where(x =>
                    x.Id == attendance.BranchId &&
                    x.TenantId == attendance.TenantId &&
                    x.IsActive)
                .Select(x => new
                {
                    BranchStandardWorkingHours =
                        x.StandardWorkingHours,

                    BranchWorkStartTime =
                        x.WorkStartTime,

                    BranchWorkEndTime =
                        x.WorkEndTime,

                    TenantStandardWorkingHours =
                        x.Tenant.StandardWorkingHours,

                    TenantWorkStartTime =
                        x.Tenant.WorkStartTime,

                    TenantWorkEndTime =
                        x.Tenant.WorkEndTime
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (scheduleData == null)
        {
            return BadRequest(
                "Attendance branch is invalid or inactive.");
        }

        // ========================================================
        // RESOLVE SCHEDULE
        // ========================================================

        var schedule =
            ResolveSchedule(
                scheduleData.BranchStandardWorkingHours,
                scheduleData.BranchWorkStartTime,
                scheduleData.BranchWorkEndTime,
                scheduleData.TenantStandardWorkingHours,
                scheduleData.TenantWorkStartTime,
                scheduleData.TenantWorkEndTime);

        // ========================================================
        // RECALCULATE EVERYTHING
        // ========================================================

        var calculation =
            CalculateAttendance(
                attendance.CheckInTime,
                attendance.CheckOutTime,
                schedule.StandardWorkingHours,
                schedule.WorkStartTime,
                schedule.WorkEndTime);

        attendance.WorkedHours =
            calculation.WorkedHours;

        attendance.WorkingHoursBalance =
            calculation.WorkingHoursBalance;

        // ========================================================
        // STATUS
        //
        // Backend calculates status automatically.
        //
        // Requested status is intentionally NOT trusted here.
        //
        // Priority:
        //
        // 1. HalfDay
        // 2. Late
        // 3. Present
        // ========================================================

        attendance.Status =
            calculation.Status;

        // ========================================================
        // BIOMETRIC DEVICE
        // ========================================================

        if (request.BiometricDeviceId != null)
        {
            attendance.BiometricDeviceId =
                request.BiometricDeviceId;
        }

        // ========================================================
        // SAVE
        // ========================================================

        await _db.SaveChangesAsync(
            cancellationToken);

        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            message =
                "Attendance updated successfully.",

            attendanceId =
                attendance.Id,

            tenantId =
                attendance.TenantId,

            branchId =
                attendance.BranchId,

            userId =
                attendance.UserId,

            date =
                attendance.Date,

            checkInTime =
                attendance.CheckInTime,

            checkOutTime =
                attendance.CheckOutTime,

            // =====================================================
            // SCHEDULE
            // =====================================================

            requiredHours =
                schedule.StandardWorkingHours,

            scheduledStartTime =
                schedule.WorkStartTime,

            scheduledEndTime =
                schedule.WorkEndTime,

            scheduleSource =
                schedule.Source,

            // =====================================================
            // CALCULATED
            // =====================================================

            workedHours =
                calculation.WorkedHours,

            overtimeHours =
                calculation.OvertimeHours,

            workingHoursBalance =
                calculation.WorkingHoursBalance,

            // =====================================================
            // STATUS FLAGS
            // =====================================================

            isLate =
                calculation.IsLate,

            isEarlyLeaving =
                calculation.IsEarlyLeaving,

            isHalfDay =
                calculation.IsHalfDay,

            // =====================================================
            // STATUS
            // =====================================================

            statusId =
                (int)attendance.Status,

            status =
                attendance.Status.ToString(),

            biometricDeviceId =
                attendance.BiometricDeviceId
        });
    }

    // ============================================================
    // POST /api/Attendance/batch/validate
    //
    // Reads parsed Excel rows sent by the browser.
    // Does NOT receive or store the Excel file.
    //
    // HR / Company Admin / Branch Manager can validate.
    // ============================================================

    [HttpPost("batch/validate")]
    public async Task<IActionResult> ValidateAttendanceBatch(
        [FromBody] ValidateAttendanceBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId =
            _tenantService.UserId.Value;

        var currentUser =
            await _db.Users
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
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        var isCompanyAdmin =
            currentUser.RoleNumber == 2;

        var isHrOps =
            currentUser.RoleNumber == 3;

        var isBranchManager =
            currentUser.RoleNumber == 4;

        if (!isCompanyAdmin &&
            !isHrOps &&
            !isBranchManager)
        {
            return Forbid();
        }

        if (request.Rows == null ||
            request.Rows.Count == 0)
        {
            return BadRequest(
                "No attendance rows were provided.");
        }

        if (request.Rows.Count > 10000)
        {
            return BadRequest(
                "Maximum 10,000 attendance rows can be processed at once.");
        }

        // ========================================================
        // TENANT
        // ========================================================

        if (!currentUser.TenantId.HasValue)
        {
            return BadRequest(
                "Authenticated user is not assigned to a tenant.");
        }

        var tenantId =
            currentUser.TenantId.Value;

        // ========================================================
        // BRANCH MANAGER SCOPE
        // ========================================================

        int? managerBranchId =
            null;

        if (isBranchManager)
        {
            if (!currentUser.BranchId.HasValue)
            {
                return BadRequest(
                    "Branch manager is not assigned to a branch.");
            }

            managerBranchId =
                currentUser.BranchId.Value;
        }

        // ========================================================
        // LOAD USERS
        // ========================================================

        var employeeCodes =
            request.Rows
                .Select(x =>
                    x.EmployeeCode
                        .Trim()
                        .ToLower())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

        var users =
            await _db.Users
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    employeeCodes.Contains(
                        x.EmployeeCode.ToLower()))
                .Select(x => new
                {
                    x.Id,
                    x.TenantId,
                    x.BranchId,
                    x.FullName,
                    x.EmployeeCode,
                    x.IsActive
                })
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // LOAD BRANCHES + SCHEDULE
        // ========================================================

        var branchIds =
            users
                .Where(x => x.BranchId.HasValue)
                .Select(x => x.BranchId!.Value)
                .Distinct()
                .ToList();

        var branches =
            await _db.TenantBranches
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    branchIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.BranchName,
                    x.IsActive,

                    BranchStandardWorkingHours =
                        x.StandardWorkingHours,

                    BranchWorkStartTime =
                        x.WorkStartTime,

                    BranchWorkEndTime =
                        x.WorkEndTime,

                    TenantStandardWorkingHours =
                        x.Tenant.StandardWorkingHours,

                    TenantWorkStartTime =
                        x.Tenant.WorkStartTime,

                    TenantWorkEndTime =
                        x.Tenant.WorkEndTime
                })
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // EXISTING ATTENDANCE
        // ========================================================

        var dates =
            request.Rows
                .Select(x => x.Date)
                .Distinct()
                .ToList();

        var userIds =
            users
                .Select(x => x.Id)
                .ToList();

        var existing =
            await _db.AttendanceRecords
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    userIds.Contains(x.UserId) &&
                    dates.Contains(x.Date))
                .Select(x => new
                {
                    x.UserId,
                    x.Date
                })
                .ToListAsync(
                    cancellationToken);

        var existingSet =
            existing
                .Select(x =>
                    $"{x.UserId}:{x.Date}")
                .ToHashSet();

        // ========================================================
        // BUILD PREVIEW
        // ========================================================

        var previewRows =
            new List<object>();

        for (var index = 0;
             index < request.Rows.Count;
             index++)
        {
            var row =
                request.Rows[index];

            var errors =
                new List<string>();

            var warnings =
                new List<string>();

            var employeeCode =
                row.EmployeeCode.Trim();

            // ----------------------------------------------------
            // USER
            // ----------------------------------------------------

            var user =
                users.FirstOrDefault(x =>
                    string.Equals(
                        x.EmployeeCode.Trim(),
                        employeeCode,
                        StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                errors.Add(
                    "EmployeeCode does not exist in this tenant.");
            }

            if (user != null &&
                !user.IsActive)
            {
                errors.Add(
                    "Employee is inactive.");
            }

            // ----------------------------------------------------
            // BRANCH
            // ----------------------------------------------------

            var branch =
                user?.BranchId.HasValue == true
                    ? branches.FirstOrDefault(
                        x => x.Id == user.BranchId.Value)
                    : null;

            if (user != null &&
                !user.BranchId.HasValue)
            {
                errors.Add(
                    "Employee is not assigned to a branch.");
            }

            if (branch != null &&
                !branch.IsActive)
            {
                errors.Add(
                    "Employee branch is inactive.");
            }

            // ----------------------------------------------------
            // BRANCH MANAGER SCOPE
            // ----------------------------------------------------

            if (managerBranchId.HasValue &&
                user?.BranchId != managerBranchId)
            {
                errors.Add(
                    "Employee belongs to another branch.");
            }

            // ----------------------------------------------------
            // DATE
            // ----------------------------------------------------

            if (row.Date == default)
            {
                errors.Add(
                    "Attendance date is required.");
            }

            // ----------------------------------------------------
            // CHECK-IN / CHECK-OUT
            // ----------------------------------------------------

            if (!row.CheckInTime.HasValue &&
                !row.CheckOutTime.HasValue)
            {
                errors.Add(
                    "At least CheckInTime or CheckOutTime is required.");
            }

            if (row.CheckInTime.HasValue &&
                row.CheckOutTime.HasValue &&
                row.CheckOutTime.Value <
                row.CheckInTime.Value)
            {
                errors.Add(
                    "Check-out time cannot be earlier than check-in time.");
            }

            // ----------------------------------------------------
            // DUPLICATE
            // ----------------------------------------------------

            if (user != null &&
                existingSet.Contains(
                    $"{user.Id}:{row.Date}"))
            {
                errors.Add(
                    "Attendance already exists for this employee and date.");
            }

            // ----------------------------------------------------
            // CALCULATE
            // ----------------------------------------------------

            decimal workedHours = 0m;
            decimal overtimeHours = 0m;
            decimal workingHoursBalance = 0m;

            bool isLate = false;
            bool isEarlyLeaving = false;

            AttendanceStatus? status = null;

            decimal? requiredHours = null;

            TimeOnly? scheduledStartTime = null;
            TimeOnly? scheduledEndTime = null;

            string? scheduleSource = null;

            if (branch != null)
            {
                var schedule =
                    ResolveSchedule(
                        branch.BranchStandardWorkingHours,
                        branch.BranchWorkStartTime,
                        branch.BranchWorkEndTime,
                        branch.TenantStandardWorkingHours,
                        branch.TenantWorkStartTime,
                        branch.TenantWorkEndTime);

                requiredHours =
                    schedule.StandardWorkingHours;

                scheduledStartTime =
                    schedule.WorkStartTime;

                scheduledEndTime =
                    schedule.WorkEndTime;

                scheduleSource =
                    schedule.Source;

                var calculation =
                    CalculateAttendance(
                        row.CheckInTime,
                        row.CheckOutTime,
                        schedule.StandardWorkingHours,
                        schedule.WorkStartTime,
                        schedule.WorkEndTime);

                workedHours =
                    calculation.WorkedHours;

                overtimeHours =
                    calculation.OvertimeHours;

                workingHoursBalance =
                    calculation.WorkingHoursBalance;

                isLate =
                    calculation.IsLate;

                isEarlyLeaving =
                    calculation.IsEarlyLeaving;

                status =
                    calculation.Status;
            }

            previewRows.Add(new
            {
                rowNumber = index + 2,

                employeeCode,

                userId = user?.Id,
                userName = user?.FullName,

                tenantId,
                branchId = user?.BranchId,
                branchName = branch?.BranchName,

                date = row.Date,

                checkInTime = row.CheckInTime,
                checkOutTime = row.CheckOutTime,

                biometricDeviceId =
                    row.BiometricDeviceId,

                requiredHours,

                scheduledStartTime,
                scheduledEndTime,

                scheduleSource,

                workedHours,
                overtimeHours,
                workingHoursBalance,

                isLate,
                isEarlyLeaving,

                statusId =
                    status.HasValue
                        ? (int)status.Value
                        : (int?)null,

                status =
                    status?.ToString(),

                isValid =
                    errors.Count == 0,

                errors,
                warnings
            });
        }

        var validRows =
            previewRows.Count(x =>
                (bool)x.GetType()
                    .GetProperty("isValid")!
                    .GetValue(x)!);

        return Ok(new
        {
            totalRows =
                previewRows.Count,

            validRows,

            invalidRows =
                previewRows.Count - validRows,

            warningRows =
                previewRows.Count(x =>
                    ((List<string>)x.GetType()
                        .GetProperty("warnings")!
                        .GetValue(x)!)
                    .Count > 0),

            rows =
                previewRows
        });
    }

    // ============================================================
    // POST /api/Attendance/batch/submit
    //
    // IMPORTANT:
    // The Excel file is NOT stored.
    // Only validated JSON rows reach this endpoint.
    // ============================================================

    [HttpPost("batch/submit")]
    public async Task<IActionResult> SubmitAttendanceBatch(
        [FromBody] SubmitAttendanceBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId =
            _tenantService.UserId.Value;

        var currentUser =
            await _db.Users
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
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
            return Forbid();

        var isCompanyAdmin =
            currentUser.RoleNumber == 2;

        var isHrOps =
            currentUser.RoleNumber == 3;

        var isBranchManager =
            currentUser.RoleNumber == 4;

        if (!isCompanyAdmin &&
            !isHrOps &&
            !isBranchManager)
        {
            return Forbid();
        }

        if (!currentUser.TenantId.HasValue)
        {
            return BadRequest(
                "Authenticated user is not assigned to a tenant.");
        }

        if (request.Rows == null ||
            request.Rows.Count == 0)
        {
            return BadRequest(
                "No attendance rows were provided.");
        }

        var tenantId =
            currentUser.TenantId.Value;

        int? managerBranchId =
            isBranchManager
                ? currentUser.BranchId
                : null;

        if (isBranchManager &&
            !managerBranchId.HasValue)
        {
            return BadRequest(
                "Branch manager is not assigned to a branch.");
        }

        // ========================================================
        // LOAD EMPLOYEES
        // ========================================================

        var employeeCodes =
            request.Rows
                .Select(x =>
                    x.EmployeeCode.Trim().ToLower())
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

        var users =
            await _db.Users
                .Where(x =>
                    x.TenantId == tenantId &&
                    employeeCodes.Contains(
                        x.EmployeeCode.ToLower()) &&
                    x.IsActive)
                .Select(x => new
                {
                    x.Id,
                    x.TenantId,
                    x.BranchId,
                    x.EmployeeCode,
                    x.FullName
                })
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // LOAD BRANCHES
        // ========================================================

        var branchIds =
            users
                .Where(x =>
                    x.BranchId.HasValue)
                .Select(x =>
                    x.BranchId!.Value)
                .Distinct()
                .ToList();

        var branches =
            await _db.TenantBranches
                .Where(x =>
                    x.TenantId == tenantId &&
                    branchIds.Contains(x.Id) &&
                    x.IsActive)
                .Select(x => new
                {
                    x.Id,

                    BranchStandardWorkingHours =
                        x.StandardWorkingHours,

                    BranchWorkStartTime =
                        x.WorkStartTime,

                    BranchWorkEndTime =
                        x.WorkEndTime,

                    TenantStandardWorkingHours =
                        x.Tenant.StandardWorkingHours,

                    TenantWorkStartTime =
                        x.Tenant.WorkStartTime,

                    TenantWorkEndTime =
                        x.Tenant.WorkEndTime
                })
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // TRANSACTION
        // ========================================================

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                cancellationToken);

        var resultRows =
            new List<object>();

        var insertedCount = 0;
        var skippedCount = 0;

        foreach (var row in request.Rows)
        {
            var employeeCode =
                row.EmployeeCode.Trim();

            var user =
                users.FirstOrDefault(x =>
                    string.Equals(
                        x.EmployeeCode.Trim(),
                        employeeCode,
                        StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                skippedCount++;

                resultRows.Add(new
                {
                    rowNumber = 0,
                    employeeCode,
                    date = row.Date,
                    attendanceId = (int?)null,
                    status = "Skipped",
                    message =
                        "Employee not found or inactive."
                });

                continue;
            }

            // ====================================================
            // BRANCH MANAGER SECURITY
            // ====================================================

            if (managerBranchId.HasValue &&
                user.BranchId != managerBranchId.Value)
            {
                skippedCount++;

                resultRows.Add(new
                {
                    rowNumber = 0,
                    employeeCode,
                    date = row.Date,
                    attendanceId = (int?)null,
                    status = "Skipped",
                    message =
                        "Employee belongs to another branch."
                });

                continue;
            }

            if (!user.BranchId.HasValue)
            {
                skippedCount++;

                resultRows.Add(new
                {
                    rowNumber = 0,
                    employeeCode,
                    date = row.Date,
                    attendanceId = (int?)null,
                    status = "Skipped",
                    message =
                        "Employee is not assigned to a branch."
                });

                continue;
            }

            var branch =
                branches.FirstOrDefault(
                    x => x.Id == user.BranchId.Value);

            if (branch == null)
            {
                skippedCount++;

                resultRows.Add(new
                {
                    rowNumber = 0,
                    employeeCode,
                    date = row.Date,
                    attendanceId = (int?)null,
                    status = "Skipped",
                    message =
                        "Employee branch is invalid or inactive."
                });

                continue;
            }

            // ====================================================
            // DUPLICATE CHECK
            // ====================================================

            var duplicate =
                await _db.AttendanceRecords
                    .AnyAsync(
                        x =>
                            x.TenantId == tenantId &&
                            x.BranchId == user.BranchId.Value &&
                            x.UserId == user.Id &&
                            x.Date == row.Date,
                        cancellationToken);

            if (duplicate)
            {
                skippedCount++;

                resultRows.Add(new
                {
                    rowNumber = 0,
                    employeeCode,
                    date = row.Date,
                    attendanceId = (int?)null,
                    status = "Skipped",
                    message =
                        "Attendance already exists."
                });

                continue;
            }

            // ====================================================
            // VALIDATE TIMES
            // ====================================================

            if (!row.CheckInTime.HasValue &&
                !row.CheckOutTime.HasValue)
            {
                skippedCount++;

                resultRows.Add(new
                {
                    rowNumber = 0,
                    employeeCode,
                    date = row.Date,
                    attendanceId = (int?)null,
                    status = "Skipped",
                    message =
                        "Check-in or check-out is required."
                });

                continue;
            }

            if (row.CheckInTime.HasValue &&
                row.CheckOutTime.HasValue &&
                row.CheckOutTime.Value <
                row.CheckInTime.Value)
            {
                skippedCount++;

                resultRows.Add(new
                {
                    rowNumber = 0,
                    employeeCode,
                    date = row.Date,
                    attendanceId = (int?)null,
                    status = "Skipped",
                    message =
                        "Check-out cannot be earlier than check-in."
                });

                continue;
            }

            // ====================================================
            // RESOLVE SCHEDULE
            // ====================================================

            var schedule =
                ResolveSchedule(
                    branch.BranchStandardWorkingHours,
                    branch.BranchWorkStartTime,
                    branch.BranchWorkEndTime,
                    branch.TenantStandardWorkingHours,
                    branch.TenantWorkStartTime,
                    branch.TenantWorkEndTime);

            // ====================================================
            // CALCULATE EVERYTHING
            // ====================================================

            var calculation =
                CalculateAttendance(
                    row.CheckInTime,
                    row.CheckOutTime,
                    schedule.StandardWorkingHours,
                    schedule.WorkStartTime,
                    schedule.WorkEndTime);

            // ====================================================
            // INSERT
            // ====================================================

            var attendance =
                new AttendanceRecord
                {
                    TenantId =
                        tenantId,

                    BranchId =
                        user.BranchId.Value,

                    UserId =
                        user.Id,

                    Date =
                        row.Date,

                    CheckInTime =
                        row.CheckInTime,

                    CheckOutTime =
                        row.CheckOutTime,

                    Status =
                        calculation.Status,

                    WorkedHours =
                        calculation.WorkedHours,

                    WorkingHoursBalance =
                        calculation.WorkingHoursBalance,

                    BiometricDeviceId =
                        row.BiometricDeviceId
                };

            _db.AttendanceRecords.Add(
                attendance);

            insertedCount++;

            resultRows.Add(new
            {
                rowNumber = 0,
                employeeCode,
                date = row.Date,
                attendanceId = (int?)null,
                status = "Inserted",
                message =
                    "Attendance inserted successfully."
            });
        }

        await _db.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return Ok(new
        {
            message =
                "Attendance batch processed successfully.",

            insertedCount,

            skippedCount,

            rows =
                resultRows
        });
    }


    // ============================================================
    // SCHEDULE RESOLUTION
    //
    // Branch schedule is used ONLY when all three values exist.
    //
    // Otherwise tenant schedule is used.
    // ============================================================

    private static AttendanceSchedule ResolveSchedule(
        decimal? branchStandardWorkingHours,
        TimeOnly? branchWorkStartTime,
        TimeOnly? branchWorkEndTime,
        decimal tenantStandardWorkingHours,
        TimeOnly tenantWorkStartTime,
        TimeOnly tenantWorkEndTime)
    {
        var branchHasCompleteSchedule =
            branchStandardWorkingHours.HasValue &&
            branchWorkStartTime.HasValue &&
            branchWorkEndTime.HasValue;

        if (branchHasCompleteSchedule)
        {
            return new AttendanceSchedule
            {
                StandardWorkingHours =
                    branchStandardWorkingHours!.Value,

                WorkStartTime =
                    branchWorkStartTime!.Value,

                WorkEndTime =
                    branchWorkEndTime!.Value,

                Source =
                    "Branch"
            };
        }

        return new AttendanceSchedule
        {
            StandardWorkingHours =
                tenantStandardWorkingHours,

            WorkStartTime =
                tenantWorkStartTime,

            WorkEndTime =
                tenantWorkEndTime,

            Source =
                "Tenant"
        };
    }

    // ============================================================
    // LATE CHECK
    //
    // Example:
    //
    // Schedule = 09:00
    //
    // 09:00 -> false
    // 09:05 -> false
    // 09:10 -> false
    // 09:11 -> true
    // ============================================================

    private static bool IsLate(
        TimeOnly? checkInTime,
        TimeOnly scheduledStartTime)
    {
        if (!checkInTime.HasValue)
            return false;

        var graceEnd =
            scheduledStartTime.AddMinutes(
                GracePeriodMinutes);

        return checkInTime.Value > graceEnd;
    }

    // ============================================================
    // EARLY LEAVING CHECK
    //
    // Example:
    //
    // Schedule ends = 17:00
    //
    // 17:00 -> false
    // 16:55 -> false
    // 16:50 -> false
    // 16:49 -> true
    // ============================================================

    private static bool IsEarlyLeaving(
        TimeOnly? checkOutTime,
        TimeOnly scheduledEndTime)
    {
        if (!checkOutTime.HasValue)
            return false;

        var graceStart =
            scheduledEndTime.AddMinutes(
                -GracePeriodMinutes);

        return checkOutTime.Value < graceStart;
    }

    // ============================================================
    // ATTENDANCE CALCULATION
    //
    // WorkedHours:
    //     CheckOut - CheckIn
    //
    // Overtime:
    //     max(0, WorkedHours - RequiredHours)
    //
    // WorkingHoursBalance:
    //     WorkedHours - RequiredHours
    //
    // Late:
    //     CheckIn > Start + 10 minutes
    //
    // Early:
    //     CheckOut < End - 10 minutes
    //
    // HalfDay:
    //     WorkedHours < RequiredHours / 2
    //
    // IMPORTANT:
    // Grace period does NOT modify actual worked hours.
    // ============================================================

    private static AttendanceCalculation
        CalculateAttendance(
            TimeOnly? checkInTime,
            TimeOnly? checkOutTime,
            decimal requiredHours,
            TimeOnly scheduledStartTime,
            TimeOnly scheduledEndTime)
    {
        // ========================================================
        // WORKED HOURS
        // ========================================================

        decimal workedHours = 0m;

        if (checkInTime.HasValue &&
            checkOutTime.HasValue)
        {
            var start =
                checkInTime.Value;

            var end =
                checkOutTime.Value;

            // ====================================================
            // NORMAL SAME-DAY SHIFT
            // ====================================================

            if (end >= start)
            {
                workedHours =
                    Math.Round(
                        (decimal)(
                            end - start
                        ).TotalHours,
                        2,
                        MidpointRounding.AwayFromZero);
            }
            else
            {
                // =================================================
                // OVERNIGHT SHIFT
                //
                // Example:
                //
                // 22:00 -> 06:00
                //
                // 8 hours
                // =================================================

                var startMinutes =
                    start.Hour * 60 +
                    start.Minute;

                var endMinutes =
                    end.Hour * 60 +
                    end.Minute +
                    (24 * 60);

                var workedMinutes =
                    endMinutes - startMinutes;

                workedHours =
                    Math.Round(
                        workedMinutes / 60m,
                        2,
                        MidpointRounding.AwayFromZero);
            }
        }

        // ========================================================
        // OVERTIME
        // ========================================================

        var overtimeHours =
            Math.Max(
                0m,
                workedHours -
                requiredHours);

        overtimeHours =
            Math.Round(
                overtimeHours,
                2,
                MidpointRounding.AwayFromZero);

        // ========================================================
        // WORKING HOURS BALANCE
        // ========================================================

        var workingHoursBalance =
            Math.Round(
                workedHours -
                requiredHours,
                2,
                MidpointRounding.AwayFromZero);

        // ========================================================
        // LATE
        // ========================================================

        var isLate =
            IsLate(
                checkInTime,
                scheduledStartTime);

        // ========================================================
        // EARLY LEAVING
        // ========================================================

        var isEarlyLeaving =
            IsEarlyLeaving(
                checkOutTime,
                scheduledEndTime);

        // ========================================================
        // HALF DAY
        //
        // IMPORTANT:
        //
        // HalfDay is only determined after checkout because
        // worked hours are required.
        //
        // Example:
        //
        // Required = 8
        // Half-day threshold = 4
        //
        // 3.99 -> HalfDay
        // 4.00 -> Present/Late depending on check-in
        // ========================================================

        var halfDayThreshold =
            requiredHours / 2m;

        var isHalfDay =
            checkInTime.HasValue &&
            checkOutTime.HasValue &&
            workedHours < halfDayThreshold;

        // ========================================================
        // FINAL STATUS
        //
        // Priority:
        //
        // 1. HalfDay
        // 2. Late
        // 3. Present
        //
        // Early leaving is represented separately through
        // IsEarlyLeaving.
        // ========================================================

        AttendanceStatus status;

        if (isHalfDay)
        {
            status =
                AttendanceStatus.HalfDay;
        }
        else if (isLate)
        {
            status =
                AttendanceStatus.Late;
        }
        else
        {
            status =
                AttendanceStatus.Present;
        }

        return new AttendanceCalculation
        {
            WorkedHours =
                workedHours,

            OvertimeHours =
                overtimeHours,

            WorkingHoursBalance =
                workingHoursBalance,

            IsLate =
                isLate,

            IsEarlyLeaving =
                isEarlyLeaving,

            IsHalfDay =
                isHalfDay,

            Status =
                status
        };
    }

    // ============================================================
    // INTERNAL SCHEDULE MODEL
    // ============================================================

    private sealed class AttendanceSchedule
    {
        public decimal StandardWorkingHours { get; set; }

        public TimeOnly WorkStartTime { get; set; }

        public TimeOnly WorkEndTime { get; set; }

        public string Source { get; set; } = "Tenant";
    }

    // ============================================================
    // INTERNAL CALCULATION MODEL
    // ============================================================

    private sealed class AttendanceCalculation
    {
        public decimal WorkedHours { get; set; }

        public decimal OvertimeHours { get; set; }

        public decimal WorkingHoursBalance { get; set; }

        public bool IsLate { get; set; }

        public bool IsEarlyLeaving { get; set; }

        public bool IsHalfDay { get; set; }

        public AttendanceStatus Status { get; set; }
    }
}

// =================================================================
// REQUEST MODELS
// =================================================================

public class CheckInRequest
{
    // Kept for compatibility with the existing frontend/API.
    //
    // CheckIn() DOES NOT use this value.
    // Backend always uses DateTime.Now.
    public DateOnly? Date { get; set; }

    // Kept for compatibility.
    //
    // CheckIn() DOES NOT use this value.
    // Backend always uses DateTime.Now.
    public TimeOnly? CheckInTime { get; set; }

    public string? BiometricDeviceId { get; set; }
}

// =================================================================
// CHECK-OUT REQUEST
// =================================================================

public class CheckOutRequest
{
    // Kept for compatibility.
    //
    // CheckOut() DOES NOT use this value.
    // Backend always uses DateTime.Now.
    public DateOnly? Date { get; set; }

    // Kept for compatibility.
    //
    // CheckOut() DOES NOT use this value.
    // Backend always uses DateTime.Now.
    public TimeOnly? CheckOutTime { get; set; }
}

// =================================================================
// UPDATE ATTENDANCE REQUEST
//
// Used by management for manual correction.
//
// Overtime is NEVER accepted from frontend.
// Backend recalculates it.
// =================================================================

public class UpdateAttendanceRequest
{
    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    // Kept for backwards compatibility, but the backend does NOT
    // trust this value. Status is recalculated automatically.
    public string? Status { get; set; }

    public string? BiometricDeviceId { get; set; }
}

// =================================================================
// ATTENDANCE BATCH MODELS
// =================================================================

public class AttendanceBatchRowRequest
{
    public string EmployeeCode { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public TimeOnly? CheckInTime { get; set; }

    public TimeOnly? CheckOutTime { get; set; }

    public string? BiometricDeviceId { get; set; }
}

public class ValidateAttendanceBatchRequest
{
    public List<AttendanceBatchRowRequest> Rows { get; set; }
        = new();
}

public class SubmitAttendanceBatchRequest
{
    public List<AttendanceBatchRowRequest> Rows { get; set; }
        = new();
}