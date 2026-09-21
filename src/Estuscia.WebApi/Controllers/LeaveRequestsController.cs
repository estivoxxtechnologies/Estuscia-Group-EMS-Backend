using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaveRequestsController : ControllerBase
{
    private readonly IAppDbContext _context;
    private readonly ICurrentTenantService _tenantService;

    public LeaveRequestsController(
        IAppDbContext context,
        ICurrentTenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    // ============================================================
    // GET: api/LeaveRequests/my
    // ============================================================
    //
    // Employee sees their own leave requests.
    //
    // ============================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyLeaveRequests(
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var userId = _tenantService.UserId.Value;

        var requests = await _context.LeaveRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new
            {
                x.Id,

                x.UserId,

                EmployeeName = x.User != null
                    ? x.User.FullName
                    : string.Empty,

                x.TenantId,
                x.BranchId,

                BranchName = x.Branch != null
                    ? x.Branch.BranchName
                    : string.Empty,

                x.LeaveType,

                LeaveTypeName = x.LeaveType.ToString(),

                x.StartDate,
                x.EndDate,
                x.RequestedDays,

                x.Reason,

                x.Status,

                StatusName = x.Status.ToString(),

                x.MedicalCertificateFileUrl,
                x.MedicalCertificateFileName,

                x.ReviewedByUserId,
                ReviewedByName = x.ReviewedByUser != null
                    ? x.ReviewedByUser.FullName
                    : null,

                x.ReviewedAtUtc,
                x.ReviewReason,

                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(requests);
    }

    // ============================================================
    // GET: api/LeaveRequests
    // ============================================================
    //
    // HR Ops:
    //     Can view tenant leave requests.
    //
    // Branch Manager:
    //     Can view leave requests for their branch.
    //
    // Company Admin:
    //     Can view tenant leave requests.
    //
    // Employee:
    //     Only their own requests are returned.
    //
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetLeaveRequests(
        [FromQuery] int? branchId,
        [FromQuery] LeaveRequestStatus? status,
        [FromQuery] LeaveType? leaveType,
        [FromQuery] int? userId,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        var currentUser = await _context.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        var roleName = currentUser.Role.RoleName;

        var query = _context.LeaveRequests
            .AsNoTracking()
            .AsQueryable();

        // ========================================================
        // EMPLOYEE-LEVEL SECURITY
        // ========================================================

        if (roleName != "super_admin" &&
            roleName != "company_admin" &&
            roleName != "hr_ops" &&
            roleName != "branch_manager")
        {
            query = query.Where(x =>
                x.UserId == currentUserId);
        }

        // ========================================================
        // BRANCH MANAGER
        // ========================================================

        if (roleName == "branch_manager")
        {
            if (!currentUser.BranchId.HasValue)
            {
                return BadRequest(new
                {
                    message = "Branch Manager is not assigned to a branch."
                });
            }

            query = query.Where(x =>
                x.BranchId == currentUser.BranchId.Value);
        }

        // ========================================================
        // BRANCH FILTER
        // ========================================================

        if (branchId.HasValue)
        {
            // Branch Manager cannot request another branch.
            if (roleName == "branch_manager" &&
                branchId.Value != currentUser.BranchId)
            {
                return Forbid();
            }

            query = query.Where(x =>
                x.BranchId == branchId.Value);
        }

        // ========================================================
        // USER FILTER
        // ========================================================

        if (userId.HasValue)
        {
            // Normal employees cannot inspect another employee.
            if (roleName != "super_admin" &&
                roleName != "company_admin" &&
                roleName != "hr_ops" &&
                roleName != "branch_manager")
            {
                if (userId.Value != currentUserId)
                    return Forbid();
            }

            query = query.Where(x =>
                x.UserId == userId.Value);
        }

        // ========================================================
        // STATUS FILTER
        // ========================================================

        if (status.HasValue)
        {
            query = query.Where(x =>
                x.Status == status.Value);
        }

        // ========================================================
        // LEAVE TYPE FILTER
        // ========================================================

        if (leaveType.HasValue)
        {
            query = query.Where(x =>
                x.LeaveType == leaveType.Value);
        }

        var requests = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,

                x.UserId,

                EmployeeName = x.User != null
                    ? x.User.FullName
                    : string.Empty,

                EmployeeCode = x.User != null
                    ? x.User.EmployeeCode
                    : string.Empty,

                x.TenantId,
                x.BranchId,

                BranchName = x.Branch != null
                    ? x.Branch.BranchName
                    : string.Empty,

                x.LeaveType,
                LeaveTypeName = x.LeaveType.ToString(),

                x.StartDate,
                x.EndDate,
                x.RequestedDays,

                x.Reason,

                x.Status,
                StatusName = x.Status.ToString(),

                x.MedicalCertificateFileUrl,
                x.MedicalCertificateFileName,

                x.ReviewedByUserId,

                ReviewedByName = x.ReviewedByUser != null
                    ? x.ReviewedByUser.FullName
                    : null,

                x.ReviewedAtUtc,
                x.ReviewReason,

                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(requests);
    }

    // ============================================================
    // GET: api/LeaveRequests/{id}
    // ============================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetLeaveRequest(
        int id,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        var currentUser = await _context.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        var request = await _context.LeaveRequests
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,

                x.UserId,

                EmployeeName = x.User != null
                    ? x.User.FullName
                    : string.Empty,

                EmployeeCode = x.User != null
                    ? x.User.EmployeeCode
                    : string.Empty,

                x.TenantId,
                x.BranchId,

                BranchName = x.Branch != null
                    ? x.Branch.BranchName
                    : string.Empty,

                x.LeaveType,
                LeaveTypeName = x.LeaveType.ToString(),

                x.StartDate,
                x.EndDate,
                x.RequestedDays,

                x.Reason,

                x.Status,
                StatusName = x.Status.ToString(),

                x.MedicalCertificateFileUrl,
                x.MedicalCertificateFileName,

                x.ReviewedByUserId,

                ReviewedByName = x.ReviewedByUser != null
                    ? x.ReviewedByUser.FullName
                    : null,

                x.ReviewedAtUtc,
                x.ReviewReason,

                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (request == null)
            return NotFound(new
            {
                message = "Leave request not found."
            });

        // ========================================================
        // ACCESS CONTROL
        // ========================================================

        var roleName = currentUser.Role.RoleName;

        if (roleName == "branch_manager")
        {
            if (currentUser.BranchId != request.BranchId)
                return Forbid();
        }
        else if (roleName != "super_admin" &&
                 roleName != "company_admin" &&
                 roleName != "hr_ops")
        {
            if (request.UserId != currentUserId)
                return Forbid();
        }

        return Ok(request);
    }

    // ============================================================
    // POST: api/LeaveRequests
    // ============================================================
    //
    // Employee submits their own leave.
    //
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> CreateLeaveRequest(
        [FromBody] CreateLeaveRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        if (!_tenantService.TenantId.HasValue)
        {
            return Unauthorized();
        }

        var currentUserId = _tenantService.UserId.Value;
        var tenantId = _tenantService.TenantId.Value;

        // ========================================================
        // VALIDATE DATES
        // ========================================================

        if (request.StartDate > request.EndDate)
        {
            return BadRequest(new
            {
                message = "Start date cannot be after end date."
            });
        }

        // ========================================================
        // LOAD CURRENT USER
        // ========================================================

        var currentUser = await _context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
        {
            return BadRequest(new
            {
                message = "Inactive users cannot submit leave requests."
            });
        }

        // ========================================================
        // BRANCH IS REQUIRED
        // ========================================================

        if (!currentUser.BranchId.HasValue)
        {
            return BadRequest(new
            {
                message =
                    "You must be assigned to a branch before submitting leave."
            });
        }

        var branchId = currentUser.BranchId.Value;

        // ========================================================
        // VALIDATE BRANCH
        // ========================================================

        var branchExists = await _context.TenantBranches
            .AnyAsync(
                x => x.TenantId == tenantId &&
                     x.Id == branchId,
                cancellationToken);

        if (!branchExists)
        {
            return BadRequest(new
            {
                message = "Your assigned branch could not be found."
            });
        }

        // ========================================================
        // CALCULATE REQUESTED DAYS
        // ========================================================
        //
        // Inclusive date calculation.
        //
        // Example:
        // 10 Sep -> 10 Sep = 1 day
        // 10 Sep -> 12 Sep = 3 days
        //
        // Later we can enhance this to exclude weekends/holidays
        // if your company policy requires it.
        //
        // ========================================================

        var requestedDays =
            request.EndDate.DayNumber -
            request.StartDate.DayNumber +
            1;

        if (requestedDays <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid leave duration."
            });
        }

        // ========================================================
        // SICK LEAVE VALIDATION
        // ========================================================
        //
        // Certificate upload infrastructure will be connected
        // in the next step.
        //
        // For now, the request must indicate a certificate.
        //
        // ========================================================

        if (request.LeaveType == LeaveType.Sick &&
            string.IsNullOrWhiteSpace(request.MedicalCertificateFileUrl))
        {
            return BadRequest(new
            {
                message =
                    "A medical certificate is required for sick leave."
            });
        }

        // ========================================================
        // CHECK OVERLAPPING LEAVE
        // ========================================================
        //
        // Pending and Approved requests block another request
        // for overlapping dates.
        //
        // Rejected/Cancelled requests do not.
        //
        // ========================================================

        var hasOverlap = await _context.LeaveRequests
            .AnyAsync(x =>
                x.UserId == currentUserId &&

                (x.Status == LeaveRequestStatus.Pending ||
                 x.Status == LeaveRequestStatus.Approved) &&

                x.StartDate <= request.EndDate &&
                x.EndDate >= request.StartDate,
                cancellationToken);

        if (hasOverlap)
        {
            return Conflict(new
            {
                message =
                    "You already have a pending or approved leave request overlapping these dates."
            });
        }

        // ========================================================
        // CREATE REQUEST
        // ========================================================

        var leaveRequest = new LeaveRequest
        {
            TenantId = tenantId,
            BranchId = branchId,
            UserId = currentUserId,

            LeaveType = request.LeaveType,

            StartDate = request.StartDate,
            EndDate = request.EndDate,

            RequestedDays = requestedDays,

            Reason = request.Reason.Trim(),

            Status = LeaveRequestStatus.Pending,

            MedicalCertificateFileUrl =
                string.IsNullOrWhiteSpace(
                    request.MedicalCertificateFileUrl)
                    ? null
                    : request.MedicalCertificateFileUrl.Trim(),

            MedicalCertificateFileName =
                string.IsNullOrWhiteSpace(
                    request.MedicalCertificateFileName)
                    ? null
                    : request.MedicalCertificateFileName.Trim(),

            CreatedAtUtc = DateTime.UtcNow,

            CreatedByUserId = currentUserId
        };

        _context.LeaveRequests.Add(leaveRequest);

        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetLeaveRequest),
            new { id = leaveRequest.Id },
            new
            {
                message = "Leave request submitted successfully.",

                leaveRequest.Id,

                leaveRequest.UserId,
                leaveRequest.TenantId,
                leaveRequest.BranchId,

                leaveRequest.LeaveType,

                LeaveTypeName =
                    leaveRequest.LeaveType.ToString(),

                leaveRequest.StartDate,
                leaveRequest.EndDate,
                leaveRequest.RequestedDays,

                leaveRequest.Reason,

                leaveRequest.Status,

                StatusName =
                    leaveRequest.Status.ToString(),

                leaveRequest.MedicalCertificateFileUrl,
                leaveRequest.MedicalCertificateFileName,

                leaveRequest.CreatedAtUtc
            });
    }

    // ============================================================
    // POST: api/LeaveRequests/{id}/approve
    // ============================================================
    //
    // ONLY HR OPS can approve.
    //
    // Company Admin cannot approve.
    // Branch Manager cannot approve.
    //
    // ============================================================

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> ApproveLeave(
        int id,
        [FromBody] ReviewLeaveRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        var currentUser = await _context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        // ========================================================
        // ONLY HR OPS
        // ========================================================

        var roleName = currentUser.Role.RoleName.Trim().ToLowerInvariant();
        if (roleName != "hr_ops" && roleName != "branch_manager") { return Forbid(); }

        var leaveRequest = await _context.LeaveRequests
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (roleName == "branch_manager") { if (!currentUser.BranchId.HasValue) { return BadRequest(new { message = "Branch Manager is not assigned to a branch." }); } if (leaveRequest.BranchId != currentUser.BranchId.Value) { return Forbid(); } }

        if (leaveRequest == null)
        {
            return NotFound(new
            {
                message = "Leave request not found."
            });
        }

        // ========================================================
        // ONLY PENDING REQUESTS CAN BE APPROVED
        // ========================================================

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return BadRequest(new
            {
                message =
                    $"This leave request is already {leaveRequest.Status}."
            });
        }

        // ========================================================
        // APPROVE
        // ========================================================

        leaveRequest.Status =
            LeaveRequestStatus.Approved;

        leaveRequest.ReviewedByUserId =
            currentUserId;

        leaveRequest.ReviewedAtUtc =
            DateTime.UtcNow;

        leaveRequest.ReviewReason =
            string.IsNullOrWhiteSpace(request.ReviewReason)
                ? null
                : request.ReviewReason.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Leave request approved successfully.",

            leaveRequest.Id,

            leaveRequest.Status,

            StatusName =
                leaveRequest.Status.ToString(),

            leaveRequest.ReviewedByUserId,
            leaveRequest.ReviewedAtUtc,
            leaveRequest.ReviewReason
        });
    }

    // ============================================================
    // POST: api/LeaveRequests/{id}/reject
    // ============================================================

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> RejectLeave(
        int id,
        [FromBody] ReviewLeaveRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        var currentUser = await _context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        // ========================================================
        // ONLY HR OPS
        // ========================================================

        var roleName = currentUser.Role.RoleName.Trim().ToLowerInvariant(); 
        if (roleName != "hr_ops" && roleName != "branch_manager") { return Forbid(); }
        // ========================================================
        // REJECTION REASON REQUIRED
        // ========================================================

        if (string.IsNullOrWhiteSpace(request.ReviewReason))
        {
            return BadRequest(new
            {
                message =
                    "A rejection reason is required."
            });
        }

        var leaveRequest = await _context.LeaveRequests
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (roleName == "branch_manager")
        {
            if (!currentUser.BranchId.HasValue)
            {
                return BadRequest(new
                {
                    message =
                        "Branch Manager is not assigned to a branch."
                });
            }

            if (leaveRequest.BranchId != currentUser.BranchId.Value)
            {
                return Forbid();
            }
        }

        if (leaveRequest == null)
        {
            return NotFound(new
            {
                message = "Leave request not found."
            });
        }

        // ========================================================
        // ONLY PENDING REQUESTS CAN BE REJECTED
        // ========================================================

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return BadRequest(new
            {
                message =
                    $"This leave request is already {leaveRequest.Status}."
            });
        }

        // ========================================================
        // REJECT
        // ========================================================

        leaveRequest.Status =
            LeaveRequestStatus.Rejected;

        leaveRequest.ReviewedByUserId =
            currentUserId;

        leaveRequest.ReviewedAtUtc =
            DateTime.UtcNow;

        leaveRequest.ReviewReason =
            request.ReviewReason.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Leave request rejected successfully.",

            leaveRequest.Id,

            leaveRequest.Status,

            StatusName =
                leaveRequest.Status.ToString(),

            leaveRequest.ReviewedByUserId,
            leaveRequest.ReviewedAtUtc,
            leaveRequest.ReviewReason
        });
    }

    // ============================================================
    // POST: api/LeaveRequests/{id}/cancel
    // ============================================================
    //
    // Employee can cancel their own pending leave.
    //
    // ============================================================

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelLeave(
        int id,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        var leaveRequest = await _context.LeaveRequests
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (leaveRequest == null)
        {
            return NotFound(new
            {
                message = "Leave request not found."
            });
        }

        // ========================================================
        // ONLY OWNER CAN CANCEL
        // ========================================================

        if (leaveRequest.UserId != currentUserId)
        {
            return Forbid();
        }

        // ========================================================
        // ONLY PENDING
        // ========================================================

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return BadRequest(new
            {
                message =
                    "Only pending leave requests can be cancelled."
            });
        }

        leaveRequest.Status =
            LeaveRequestStatus.Cancelled;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Leave request cancelled successfully.",

            leaveRequest.Id,

            leaveRequest.Status,

            StatusName =
                leaveRequest.Status.ToString()
        });
    }
}

// ====================================================================
// REQUEST DTOs
// ====================================================================

public class CreateLeaveRequestRequest
{
    public LeaveType LeaveType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    // Temporary until the actual file-upload endpoint is connected.
    public string? MedicalCertificateFileUrl { get; set; }

    public string? MedicalCertificateFileName { get; set; }
}

// ====================================================================

public class ReviewLeaveRequestRequest
{
    public string? ReviewReason { get; set; }
}