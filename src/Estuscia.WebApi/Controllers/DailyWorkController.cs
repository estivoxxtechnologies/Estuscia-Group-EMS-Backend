using Estuscia.Application.Common.DTOs;
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
public class DailyWorkController : ControllerBase
{
    private readonly IAppDbContext _context;
    private readonly ICurrentTenantService _tenantService;

    public DailyWorkController(
        IAppDbContext context,
        ICurrentTenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    // ============================================================
    // GET DAILY WORK LOGS
    // ============================================================
    //
    // sales_staff:
    //     Own Sales reports only
    //
    // branch_manager:
    //     Assigned branch only
    //
    // hr_ops:
    //     Entire tenant
    //     Optional branch filter
    //
    // company_admin:
    //     Entire tenant
    //     Optional branch filter
    //
    // super_admin:
    //     No Daily Work access
    //
    // ============================================================

    [HttpGet]
    [Authorize(Roles = "sales_staff,hr_ops,branch_manager,company_admin")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int? branchId,
        [FromQuery] DateOnly? date,
        [FromQuery] int? workType,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        // ============================================================
        // LOAD CURRENT USER
        // ============================================================

        var currentUser = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == currentUserId)
            .Select(u => new
            {
                u.Id,
                u.IsActive,
                u.TenantId,
                u.BranchId,
                RoleName = u.Role != null
                    ? u.Role.RoleName
                    : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
        {
            return Unauthorized(new
            {
                message = "Your account is inactive."
            });
        }

        var role = currentUser.RoleName;

        // ============================================================
        // BASE QUERY
        // ============================================================

        IQueryable<DailyWorkLog> query =
            _context.DailyWorkLogs
                .AsNoTracking();

        // ============================================================
        // SALES STAFF
        // ============================================================

        if (role == "sales_staff")
        {
            query = query.Where(d =>
                d.UserId == currentUserId &&
                d.WorkType == WorkLogType.Sales);
        }

        // ============================================================
        // BRANCH MANAGER
        // ============================================================

        else if (role == "branch_manager")
        {
            if (!currentUser.BranchId.HasValue)
            {
                return BadRequest(new
                {
                    message = "Your account is not assigned to a branch."
                });
            }

            query = query.Where(d =>
                d.BranchId == currentUser.BranchId.Value);
        }

        // ============================================================
        // HR OPS / COMPANY ADMIN
        // ============================================================

        else if (role == "hr_ops" || role == "company_admin")
        {
            if (branchId.HasValue)
            {
                if (!currentUser.TenantId.HasValue)
                {
                    return BadRequest(new
                    {
                        message = "Your account is not assigned to a tenant."
                    });
                }

                var branchBelongsToTenant =
                    await _context.TenantBranches
                        .AsNoTracking()
                        .AnyAsync(
                            b =>
                                b.Id == branchId.Value &&
                                b.TenantId == currentUser.TenantId.Value,
                            cancellationToken);

                if (!branchBelongsToTenant)
                {
                    return BadRequest(new
                    {
                        message =
                            "The selected branch does not belong to your tenant."
                    });
                }

                query = query.Where(d =>
                    d.BranchId == branchId.Value);
            }
        }

        // ============================================================
        // UNKNOWN ROLE
        // ============================================================

        else
        {
            return Forbid();
        }

        // ============================================================
        // DATE FILTER
        // ============================================================

        if (date.HasValue)
        {
            var requestedDate = date.Value;

            query = query.Where(d =>
                d.WorkDate == requestedDate);
        }

        // ============================================================
        // WORK TYPE FILTER
        // ============================================================

        if (workType.HasValue)
        {
            if (!Enum.IsDefined(
                    typeof(WorkLogType),
                    workType.Value))
            {
                return BadRequest(new
                {
                    message = "Invalid work type."
                });
            }

            var requestedWorkType =
                (WorkLogType)workType.Value;

            if (role == "sales_staff" &&
                requestedWorkType != WorkLogType.Sales)
            {
                return BadRequest(new
                {
                    message =
                        "Sales Staff can only view Sales daily work."
                });
            }

            query = query.Where(d =>
                d.WorkType == requestedWorkType);
        }

        // ============================================================
        // EXECUTE DATABASE QUERY ONCE
        // ============================================================

        var logs = await query
            .OrderByDescending(d => d.WorkDate)
            .ThenByDescending(d => d.CreatedAtUtc)
            .Take(100)
            .Select(d => new
            {
                d.Id,
                d.TenantId,
                d.BranchId,
                d.UserId,
                d.WorkDate,
                d.WorkType,
                d.Narration,
                d.CallsMade,
                d.CallsConnected,
                d.LeadsRespondedWell,
                d.FollowUpsScheduled,
                d.HoursSpent,
                d.FeaturesShipped,
                d.RepositoryPrLinks,
                d.BlockersEncountered,
                d.Status,
                d.ManagerNotes,
                d.ReviewedByManagerId,
                d.CreatedAtUtc,

                UserName = d.User.FullName,
                BranchName = d.Branch.BranchName
            })
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }

    // ============================================================
    // SUBMIT SALES DAILY WORK
    // ============================================================
    //
    // ONLY sales_staff.
    //
    // TenantId
    // BranchId
    // UserId
    //
    // are derived from authenticated user.
    //
    // They are NEVER accepted from frontend.
    //
    // ============================================================

    [HttpPost("submit")]
    [Authorize(Roles = "sales_staff")]
    public async Task<IActionResult> SubmitWorkLog(
        [FromBody] SubmitWorkLogDto dto,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        // --------------------------------------------------------
        // Load authenticated user
        // --------------------------------------------------------

        var user = await _context.Users
            .Include(u => u.Branch)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(
                u => u.Id == currentUserId,
                cancellationToken);

        if (user == null)
            return Unauthorized();

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "Your account is inactive."
            });
        }

        // --------------------------------------------------------
        // Role validation
        // --------------------------------------------------------

        if (user.Role?.RoleName != "sales_staff")
            return Forbid();

        // ========================================================
        // TENANT REQUIRED
        // ========================================================

        if (!user.TenantId.HasValue)
        {
            return BadRequest(new
            {
                message =
                    "Your account is not assigned to a tenant."
            });
        }

        // ========================================================
        // BRANCH REQUIRED
        // ========================================================

        if (!user.BranchId.HasValue ||
            user.Branch == null)
        {
            return BadRequest(new
            {
                message =
                    "Your account is not assigned to a branch."
            });
        }

        // ========================================================
        // BRANCH / TENANT VALIDATION
        // ========================================================

        if (user.Branch.TenantId != user.TenantId.Value)
        {
            return BadRequest(new
            {
                message =
                    "Your account has an invalid branch configuration."
            });
        }

        if (!user.Branch.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Your assigned branch is inactive."
            });
        }

        // ========================================================
        // WORK TYPE
        // ========================================================

        if (dto.WorkType != (int)WorkLogType.Sales)
        {
            return BadRequest(new
            {
                message =
                    "Sales Staff can only submit Sales daily work."
            });
        }

        // ========================================================
        // WORK DATE
        // ========================================================

        var workDate =
            dto.WorkDate ??
            DateOnly.FromDateTime(DateTime.UtcNow);

        var today =
            DateOnly.FromDateTime(DateTime.UtcNow);

        if (workDate > today)
        {
            return BadRequest(new
            {
                message =
                    "Work date cannot be in the future."
            });
        }

        // ========================================================
        // NARRATION
        // ========================================================

        if (string.IsNullOrWhiteSpace(dto.Narration))
        {
            return BadRequest(new
            {
                message = "Narration is required."
            });
        }

        // ========================================================
        // CALL VALIDATION
        // ========================================================

        if (dto.CallsMade.HasValue &&
            dto.CallsMade.Value < 0)
        {
            return BadRequest(new
            {
                message =
                    "Calls Made cannot be negative."
            });
        }

        if (dto.CallsConnected.HasValue &&
            dto.CallsConnected.Value < 0)
        {
            return BadRequest(new
            {
                message =
                    "Calls Connected cannot be negative."
            });
        }

        if (dto.LeadsRespondedWell.HasValue &&
            dto.LeadsRespondedWell.Value < 0)
        {
            return BadRequest(new
            {
                message =
                    "Leads Responded Well cannot be negative."
            });
        }

        if (dto.FollowUpsScheduled.HasValue &&
            dto.FollowUpsScheduled.Value < 0)
        {
            return BadRequest(new
            {
                message =
                    "Follow-ups Scheduled cannot be negative."
            });
        }

        // ========================================================
        // RELATIONAL VALIDATION
        // ========================================================

        if (dto.CallsMade.HasValue &&
            dto.CallsConnected.HasValue &&
            dto.CallsConnected.Value >
                dto.CallsMade.Value)
        {
            return BadRequest(new
            {
                message =
                    "Calls Connected cannot exceed Calls Made."
            });
        }

        if (dto.CallsConnected.HasValue &&
            dto.LeadsRespondedWell.HasValue &&
            dto.LeadsRespondedWell.Value >
                dto.CallsConnected.Value)
        {
            return BadRequest(new
            {
                message =
                    "Leads Responded Well cannot exceed Calls Connected."
            });
        }

        // ========================================================
        // DUPLICATE CHECK
        // ========================================================
        //
        // One Sales report per user per work date.
        //

        var alreadySubmitted =
            await _context.DailyWorkLogs
                .AsNoTracking()
                .AnyAsync(
                    d =>
                        d.UserId == currentUserId &&
                        d.WorkDate == workDate &&
                        d.WorkType == WorkLogType.Sales,
                    cancellationToken);

        if (alreadySubmitted)
        {
            return Conflict(new
            {
                message =
                    "You have already submitted a Sales daily report for this date."
            });
        }

        // ========================================================
        // CREATE
        // ========================================================

        var workLog = new DailyWorkLog
        {
            // ----------------------------------------------------
            // SECURITY:
            // These come ONLY from authenticated user.
            // ----------------------------------------------------

            TenantId = user.TenantId.Value,
            BranchId = user.BranchId.Value,
            UserId = currentUserId,

            // ----------------------------------------------------
            // SALES WORK
            // ----------------------------------------------------

            WorkDate = workDate,
            WorkType = WorkLogType.Sales,

            Narration = dto.Narration.Trim(),

            CallsMade = dto.CallsMade,
            CallsConnected = dto.CallsConnected,
            LeadsRespondedWell = dto.LeadsRespondedWell,
            FollowUpsScheduled = dto.FollowUpsScheduled,

            // ----------------------------------------------------
            // Developer fields are not used for Sales.
            // ----------------------------------------------------

            HoursSpent = null,
            FeaturesShipped = null,
            RepositoryPrLinks = null,
            BlockersEncountered = null,

            // ----------------------------------------------------
            // Initial status
            // ----------------------------------------------------

            Status = "Submitted"
        };

        _context.DailyWorkLogs.Add(workLog);

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Handles a possible race condition where
            // two submissions happen simultaneously.

            return Conflict(new
            {
                message =
                    "A Sales daily report for this date already exists."
            });
        }

        return Ok(workLog);
    }


    // ============================================================
    // UPDATE DAILY WORK
    // ============================================================
    //
    // hr_ops:
    //     Tenant-wide
    //
    // company_admin:
    //     Tenant-wide
    //
    // branch_manager:
    //     Assigned branch only
    //
    // sales_staff:
    //     Cannot edit using this endpoint
    //
    // ============================================================

    [HttpPut("{id:int}")]
    [Authorize(Roles = "hr_ops,branch_manager,company_admin")]
    public async Task<IActionResult> UpdateWorkLog(
        int id,
        [FromBody] UpdateWorkLogDto dto,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        // --------------------------------------------------------
        // Load current user
        // --------------------------------------------------------

        var currentUser = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(
                u => u.Id == currentUserId,
                cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
        {
            return Unauthorized(new
            {
                message = "Your account is inactive."
            });
        }

        var role = currentUser.Role?.RoleName;

        // ========================================================
        // LOAD LOG
        // ========================================================
        //
        // Global tenant filter protects tenant isolation.
        //

        var log = await _context.DailyWorkLogs
            .FirstOrDefaultAsync(
                d => d.Id == id,
                cancellationToken);

        if (log == null)
        {
            return NotFound(new
            {
                message = "Daily work report not found."
            });
        }

        // ========================================================
        // BRANCH MANAGER AUTHORIZATION
        // ========================================================

        if (role == "branch_manager")
        {
            if (!currentUser.BranchId.HasValue)
            {
                return BadRequest(new
                {
                    message =
                        "Your account is not assigned to a branch."
                });
            }

            if (log.BranchId != currentUser.BranchId.Value)
            {
                return Forbid();
            }
        }

        // ========================================================
        // WORK DATE
        // ========================================================

        if (dto.WorkDate.HasValue)
        {
            var today =
                DateOnly.FromDateTime(DateTime.UtcNow);

            if (dto.WorkDate.Value > today)
            {
                return BadRequest(new
                {
                    message =
                        "Work date cannot be in the future."
                });
            }

            log.WorkDate = dto.WorkDate.Value;
        }

        // ========================================================
        // NARRATION
        // ========================================================

        if (dto.Narration != null)
        {
            if (string.IsNullOrWhiteSpace(dto.Narration))
            {
                return BadRequest(new
                {
                    message =
                        "Narration cannot be empty."
                });
            }

            log.Narration = dto.Narration.Trim();
        }

        // ========================================================
        // COUNT VALIDATION
        // ========================================================

        if (dto.CallsMade.HasValue &&
            dto.CallsMade.Value < 0)
        {
            return BadRequest(new
            {
                message =
                    "Calls Made cannot be negative."
            });
        }

        if (dto.CallsConnected.HasValue &&
            dto.CallsConnected.Value < 0)
        {
            return BadRequest(new
            {
                message =
                    "Calls Connected cannot be negative."
            });
        }

        if (dto.LeadsRespondedWell.HasValue &&
            dto.LeadsRespondedWell.Value < 0)
        {
            return BadRequest(new
            {
                message =
                    "Leads Responded Well cannot be negative."
            });
        }

        if (dto.FollowUpsScheduled.HasValue &&
            dto.FollowUpsScheduled.Value < 0)
        {
            return BadRequest(new
            {
                message =
                    "Follow-ups Scheduled cannot be negative."
            });
        }

        // --------------------------------------------------------
        // Validate against existing values when only some
        // fields are supplied.
        // --------------------------------------------------------

        var callsMade =
            dto.CallsMade ?? log.CallsMade;

        var callsConnected =
            dto.CallsConnected ?? log.CallsConnected;

        var respondedWell =
            dto.LeadsRespondedWell ??
            log.LeadsRespondedWell;

        if (callsMade.HasValue &&
            callsConnected.HasValue &&
            callsConnected.Value > callsMade.Value)
        {
            return BadRequest(new
            {
                message =
                    "Calls Connected cannot exceed Calls Made."
            });
        }

        if (callsConnected.HasValue &&
            respondedWell.HasValue &&
            respondedWell.Value > callsConnected.Value)
        {
            return BadRequest(new
            {
                message =
                    "Leads Responded Well cannot exceed Calls Connected."
            });
        }

        // ========================================================
        // APPLY CHANGES
        // ========================================================

        if (dto.CallsMade.HasValue)
            log.CallsMade = dto.CallsMade.Value;

        if (dto.CallsConnected.HasValue)
            log.CallsConnected =
                dto.CallsConnected.Value;

        if (dto.LeadsRespondedWell.HasValue)
            log.LeadsRespondedWell =
                dto.LeadsRespondedWell.Value;

        if (dto.FollowUpsScheduled.HasValue)
            log.FollowUpsScheduled =
                dto.FollowUpsScheduled.Value;

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(log);
    }


    // ============================================================
    // REVIEW DAILY WORK
    // ============================================================

    [HttpPost("{id:int}/review")]
    [Authorize(Roles = "hr_ops,branch_manager,company_admin")]
    public async Task<IActionResult> ReviewWorkLog(
        int id,
        [FromBody] ReviewDailyWorkLogDto dto,
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        // --------------------------------------------------------
        // Load current user
        // --------------------------------------------------------

        var currentUser = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(
                u => u.Id == currentUserId,
                cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!currentUser.IsActive)
        {
            return Unauthorized(new
            {
                message = "Your account is inactive."
            });
        }

        var role = currentUser.Role?.RoleName;

        // ========================================================
        // LOAD LOG
        // ========================================================

        var log = await _context.DailyWorkLogs
            .FirstOrDefaultAsync(
                d => d.Id == id,
                cancellationToken);

        if (log == null)
        {
            return NotFound(new
            {
                message = "Daily work report not found."
            });
        }

        // ========================================================
        // BRANCH MANAGER AUTHORIZATION
        // ========================================================

        if (role == "branch_manager")
        {
            if (!currentUser.BranchId.HasValue)
            {
                return BadRequest(new
                {
                    message =
                        "Your account is not assigned to a branch."
                });
            }

            if (log.BranchId != currentUser.BranchId.Value)
            {
                return Forbid();
            }
        }

        // ========================================================
        // STATUS
        // ========================================================

        var allowedStatuses = new[]
        {
            "Submitted",
            "Reviewed",
            "Rejected"
        };

        if (!allowedStatuses.Contains(
                dto.Status,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Invalid review status."
            });
        }

        // Normalize status values.

        var normalizedStatus =
            allowedStatuses.First(
                s => string.Equals(
                    s,
                    dto.Status,
                    StringComparison.OrdinalIgnoreCase));

        log.Status = normalizedStatus;

        log.ManagerNotes =
            string.IsNullOrWhiteSpace(dto.ManagerNotes)
                ? null
                : dto.ManagerNotes.Trim();

        log.ReviewedByManagerId =
            currentUserId;

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(log);
    }
}