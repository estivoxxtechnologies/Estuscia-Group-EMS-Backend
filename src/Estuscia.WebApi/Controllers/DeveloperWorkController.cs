using Estuscia.Application;
using Estuscia.Application.Common.DTOs;
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
public class DeveloperWorkController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _tenantService;

    public DeveloperWorkController(
        AppDbContext db,
        ICurrentTenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    private async Task<TenantBranch?> GetBranchAsync(
    int branchId,
    CancellationToken cancellationToken)
    {
        return await _db.TenantBranches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.Id == branchId,
                cancellationToken);
    }

    // ============================================================
    // CURRENT USER
    // ============================================================

    private async Task<ApplicationUser?> GetCurrentUserAsync(
        CancellationToken cancellationToken)
    {
        if (!_tenantService.UserId.HasValue)
            return null;

        return await _db.Users
            .IgnoreQueryFilters()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Id == _tenantService.UserId.Value,
                cancellationToken);
    }

    // ============================================================
    // ROLE HELPERS
    // ============================================================

    private static bool IsManagementRole(int roleId)
    {
        // company_admin = 2
        // hr_ops        = 3
        // branch_manager = 4

        return roleId is 2 or 3 or 4;
    }

    private static bool IsDeveloper(ApplicationUser user)
    {
        // developer = 6
        return user.RoleNumber == 6;
    }

    private static bool IsSeniorDeveloper(ApplicationUser user)
    {
        return IsDeveloper(user) &&
               string.Equals(
                   user.Designation,
                   "senior",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJuniorDeveloper(ApplicationUser user)
    {
        return IsDeveloper(user) &&
               string.Equals(
                   user.Designation,
                   "junior",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanAssignWork(ApplicationUser user)
    {
        return IsManagementRole(user.RoleNumber) ||
               IsSeniorDeveloper(user);
    }

    private static bool CanViewTeam(ApplicationUser user)
    {
        return IsManagementRole(user.RoleNumber) ||
               IsSeniorDeveloper(user);
    }

    // ============================================================
    // BRANCH ACCESS
    // ============================================================

    private bool CanAccessBranch(
        ApplicationUser currentUser,
        int branchId)
    {
        // Super admin can work across tenants/branches.
        if (currentUser.RoleNumber == 1)
            return true;

        // Company Admin / HR Ops can access all branches
        // belonging to their tenant.
        if (currentUser.RoleNumber is 2 or 3)
            return true;

        // Branch Manager / Developer can access their own branch.
        return currentUser.BranchId.HasValue &&
               currentUser.BranchId.Value == branchId;
    }

    // ============================================================
    // USER ACCESS
    // ============================================================

    private async Task<ApplicationUser?> GetDeveloperUserAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        return await _db.Users
            .IgnoreQueryFilters()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);
    }

    // ============================================================
    // STATUS TRANSITION VALIDATION
    // ============================================================

    private static bool IsValidStatusTransition(
        DeveloperWorkStatus current,
        DeveloperWorkStatus next)
    {
        if (current == next)
            return true;

        return current switch
        {
            DeveloperWorkStatus.Pending =>
                next == DeveloperWorkStatus.InProgress,

            DeveloperWorkStatus.InProgress =>
                next is
                    DeveloperWorkStatus.OnHold or
                    DeveloperWorkStatus.Completed,

            DeveloperWorkStatus.OnHold =>
                next == DeveloperWorkStatus.InProgress,

            DeveloperWorkStatus.Completed =>
                false,

            _ => false
        };
    }

    // ============================================================
    // DTO MAPPER
    // ============================================================

    private static DeveloperWorkResponseDto MapWork(
        DeveloperWork work)
    {
        return new DeveloperWorkResponseDto
        {
            Id = work.Id,

            TenantId = work.TenantId ?? 0,

            BranchId = work.BranchId,

            BranchName = work.Branch?.BranchName ?? string.Empty,

            Title = work.Title,

            Description = work.Description,

            WorkType = work.WorkType,

            Priority = work.Priority,

            Status = work.Status,

            AssignedToUserId = work.AssignedToUserId,

            AssignedToUserName =
                work.AssignedToUser?.FullName ??
                work.AssignedToUser?.Email ??
                string.Empty,

            AssignedToEmployeeCode =
                work.AssignedToUser?.EmployeeCode,

            AssignedByUserId = work.AssignedByUserId,

            AssignedByUserName =
                work.AssignedByUser?.FullName ??
                work.AssignedByUser?.Email ??
                string.Empty,

            AssignedAtUtc = work.AssignedAtUtc,

            DueDateUtc = work.DueDateUtc,

            StartedAtUtc = work.StartedAtUtc,

            CompletedAtUtc = work.CompletedAtUtc,

            CompletionNotes = work.CompletionNotes
        };
    }

    // ============================================================
    // GET MY WORK
    // ============================================================
    //
    // Junior Developer sees their own assigned work.
    //
    // GET:
    // /api/DeveloperWork/my
    //
    // Optional:
    // ?status=Pending
    // ?status=InProgress
    // ?date=2026-09-19
    // ============================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyWork(
        [FromQuery] DeveloperWorkStatus? status,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!IsJuniorDeveloper(currentUser) &&
            !IsSeniorDeveloper(currentUser))
        {
            return Forbid();
        }

        var query = _db.DeveloperWorks
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.AssignedToUser)
            .Include(x => x.AssignedByUser)
            .Where(x =>
                x.AssignedToUserId == currentUser.Id);

        if (status.HasValue)
        {
            query = query.Where(x =>
                x.Status == status.Value);
        }

        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue);

            var end = start.AddDays(1);

            query = query.Where(x =>
                x.AssignedAtUtc >= start &&
                x.AssignedAtUtc < end);
        }

        var works = await query
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.DueDateUtc)
            .ThenByDescending(x => x.AssignedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(
            works.Select(MapWork).ToList());
    }

    // ============================================================
    // GET TEAM WORK
    // ============================================================
    //
    // Senior Developer / management.
    //
    // GET:
    // /api/DeveloperWork/team
    //
    // Optional:
    // ?branchId=1
    // ?assignedToUserId=4
    // ?status=InProgress
    // ============================================================

    [HttpGet("team")]
    public async Task<IActionResult> GetTeamWork(
        [FromQuery] int? branchId,
        [FromQuery] int? assignedToUserId,
        [FromQuery] DeveloperWorkStatus? status,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!CanViewTeam(currentUser))
            return Forbid();

        var query = _db.DeveloperWorks
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.AssignedToUser)
            .Include(x => x.AssignedByUser)
            .AsQueryable();

        // --------------------------------------------------------
        // Branch scope
        // --------------------------------------------------------

        if (currentUser.RoleNumber == 1)
        {
            // Super admin may specify branch.
            if (branchId.HasValue)
            {
                query = query.Where(x =>
                    x.BranchId == branchId.Value);
            }
        }
        else if (currentUser.RoleNumber is 2 or 3)
        {
            // Company admin / HR can optionally filter branch.
            if (branchId.HasValue)
            {
                query = query.Where(x =>
                    x.BranchId == branchId.Value);
            }
        }
        else
        {
            // Senior developer / branch manager
            if (!currentUser.BranchId.HasValue)
                return Ok(new List<DeveloperWorkResponseDto>());

            query = query.Where(x =>
                x.BranchId == currentUser.BranchId.Value);
        }

        // --------------------------------------------------------
        // Assigned user
        // --------------------------------------------------------

        if (assignedToUserId.HasValue)
        {
            query = query.Where(x =>
                x.AssignedToUserId ==
                assignedToUserId.Value);
        }

        // --------------------------------------------------------
        // Status
        // --------------------------------------------------------

        if (status.HasValue)
        {
            query = query.Where(x =>
                x.Status == status.Value);
        }

        var works = await query
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.DueDateUtc)
            .ThenByDescending(x => x.AssignedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(
            works.Select(MapWork).ToList());
    }

    // ============================================================
    // GET SINGLE WORK
    // ============================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetWork(
        int id,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        var work = await _db.DeveloperWorks
            .AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.AssignedToUser)
            .Include(x => x.AssignedByUser)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (work == null)
            return NotFound();

        // --------------------------------------------------------
        // Access
        // --------------------------------------------------------

        var canAccess =
            currentUser.RoleNumber == 1 ||
            IsManagementRole(currentUser.RoleNumber) ||
            work.AssignedToUserId == currentUser.Id ||
            (
                IsSeniorDeveloper(currentUser) &&
                currentUser.BranchId.HasValue &&
                currentUser.BranchId.Value == work.BranchId
            );

        if (!canAccess)
            return Forbid();

        return Ok(MapWork(work));
    }

    // ============================================================
    // CREATE / ASSIGN WORK
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> CreateWork(
        [FromBody] CreateDeveloperWorkRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!CanAssignWork(currentUser))
            return Forbid();

        // --------------------------------------------------------
        // Validation
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new
            {
                message = "Work title is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new
            {
                message = "Work description is required."
            });
        }

        if (request.AssignedToUserId <= 0)
        {
            return BadRequest(new
            {
                message = "A developer must be selected."
            });
        }

        if (request.BranchId <= 0)
        {
            return BadRequest(new
            {
                message = "Branch is required."
            });
        }

        // --------------------------------------------------------
        // Branch permission
        // --------------------------------------------------------

        if (!CanAccessBranch(
                currentUser,
                request.BranchId))
        {
            return Forbid();
        }

        var branch = await GetBranchAsync(
    request.BranchId,
    cancellationToken);

        if (branch == null)
        {
            return BadRequest(new
            {
                message = "The selected branch was not found."
            });
        }

        if (currentUser.RoleNumber != 1 &&
            branch.TenantId != currentUser.TenantId)
        {
            return Forbid();
        }

        // --------------------------------------------------------
        // Validate assigned user
        // --------------------------------------------------------

        var assignedUser =
            await GetDeveloperUserAsync(
                request.AssignedToUserId,
                cancellationToken);

        if (assignedUser == null)
        {
            return BadRequest(new
            {
                message = "Assigned developer was not found."
            });
        }

        if (!IsDeveloper(assignedUser))
        {
            return BadRequest(new
            {
                message =
                    "Work can only be assigned to a developer."
            });
        }

        if (!assignedUser.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "The selected developer is inactive."
            });
        }

        // --------------------------------------------------------
        // Branch validation
        // --------------------------------------------------------

        if (!assignedUser.BranchId.HasValue ||
            assignedUser.BranchId.Value != request.BranchId)
        {
            return BadRequest(new
            {
                message =
                    "The selected developer does not belong to this branch."
            });
        }

        // --------------------------------------------------------
        // Senior cannot assign to another senior developer
        // --------------------------------------------------------

        if (IsSeniorDeveloper(currentUser) &&
            IsSeniorDeveloper(assignedUser))
        {
            return BadRequest(new
            {
                message =
                    "A senior developer cannot assign work to another senior developer."
            });
        }

        // --------------------------------------------------------
        // Tenant
        // --------------------------------------------------------

        int? tenantId = currentUser.TenantId;

        if (currentUser.RoleNumber == 1)
        {
            tenantId = assignedUser.TenantId;
        }

        // --------------------------------------------------------
        // Validate due date
        // --------------------------------------------------------

        if (request.DueDateUtc.HasValue &&
            request.DueDateUtc.Value < DateTime.UtcNow.AddMinutes(-1))
        {
            return BadRequest(new
            {
                message =
                    "Due date cannot be in the past."
            });
        }

        // --------------------------------------------------------
        // Create
        // --------------------------------------------------------

        var work = new DeveloperWork
        {
            TenantId = tenantId,

            BranchId = request.BranchId,

            Title = request.Title.Trim(),

            Description = request.Description.Trim(),

            WorkType = request.WorkType,

            Priority = request.Priority,

            Status = DeveloperWorkStatus.Pending,

            AssignedToUserId =
                request.AssignedToUserId,

            AssignedByUserId =
                currentUser.Id,

            AssignedAtUtc =
                DateTime.UtcNow,

            DueDateUtc =
                request.DueDateUtc,

            StartedAtUtc = null,

            CompletedAtUtc = null,

            CompletionNotes = null
        };

        _db.DeveloperWorks.Add(work);

        await _db.SaveChangesAsync(cancellationToken);

        // Reload navigation properties for response.
        work = await _db.DeveloperWorks
            .Include(x => x.Branch)
            .Include(x => x.AssignedToUser)
            .Include(x => x.AssignedByUser)
            .FirstAsync(
                x => x.Id == work.Id,
                cancellationToken);

        return CreatedAtAction(
            nameof(GetWork),
            new { id = work.Id },
            MapWork(work));
    }

    // ============================================================
    // UPDATE STATUS
    // ============================================================

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateDeveloperWorkStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        var work = await _db.DeveloperWorks
            .Include(x => x.Branch)
            .Include(x => x.AssignedToUser)
            .Include(x => x.AssignedByUser)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (work == null)
            return NotFound();

        // --------------------------------------------------------
        // Permission
        // --------------------------------------------------------

        var isOwner =
            work.AssignedToUserId == currentUser.Id;

        var isSeniorSameBranch =
            IsSeniorDeveloper(currentUser) &&
            currentUser.BranchId.HasValue &&
            currentUser.BranchId.Value == work.BranchId;

        var isManagement =
            IsManagementRole(currentUser.RoleNumber) ||
            currentUser.RoleNumber == 1;

        if (!isOwner &&
            !isSeniorSameBranch &&
            !isManagement)
        {
            return Forbid();
        }

        // --------------------------------------------------------
        // Completed work should normally not be reopened.
        // Management/senior can still move it if required.
        // --------------------------------------------------------

        if (work.Status == DeveloperWorkStatus.Completed &&
            request.Status != DeveloperWorkStatus.Completed)
        {
            if (!isManagement && !isSeniorSameBranch)
            {
                return BadRequest(new
                {
                    message =
                        "Completed work cannot be reopened by a junior developer."
                });
            }
        }

        // --------------------------------------------------------
        // Validate workflow transition
        // --------------------------------------------------------

        if (!IsValidStatusTransition(
                work.Status,
                request.Status))
        {
            return BadRequest(new
            {
                message =
                    $"Invalid status transition from {work.Status} to {request.Status}."
            });
        }

        var now = DateTime.UtcNow;

        // --------------------------------------------------------
        // Start
        // --------------------------------------------------------

        if (request.Status ==
                DeveloperWorkStatus.InProgress &&
            work.StartedAtUtc == null)
        {
            work.StartedAtUtc = now;
        }

        // --------------------------------------------------------
        // Completed
        // --------------------------------------------------------

        if (request.Status ==
                DeveloperWorkStatus.Completed)
        {
            work.CompletedAtUtc = now;

            if (!string.IsNullOrWhiteSpace(
                    request.CompletionNotes))
            {
                work.CompletionNotes =
                    request.CompletionNotes.Trim();
            }
        }
        else
        {
            work.CompletedAtUtc = null;

            if (request.CompletionNotes != null)
            {
                work.CompletionNotes =
                    string.IsNullOrWhiteSpace(
                        request.CompletionNotes)
                        ? null
                        : request.CompletionNotes.Trim();
            }
        }

        work.Status = request.Status;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(MapWork(work));
    }

    // ============================================================
    // UPDATE WORK DETAILS
    // ============================================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateWork(
        int id,
        [FromBody] UpdateDeveloperWorkRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);

        if (currentUser == null)
            return Unauthorized();

        if (!CanAssignWork(currentUser))
            return Forbid();

        var work = await _db.DeveloperWorks
            .Include(x => x.Branch)
            .Include(x => x.AssignedToUser)
            .Include(x => x.AssignedByUser)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (work == null)
            return NotFound();

        // --------------------------------------------------------
        // Senior can edit only same branch
        // --------------------------------------------------------

        if (IsSeniorDeveloper(currentUser))
        {
            if (!currentUser.BranchId.HasValue ||
                currentUser.BranchId.Value != work.BranchId)
            {
                return Forbid();
            }
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new
            {
                message = "Work title is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new
            {
                message = "Work description is required."
            });
        }

        if (work.Status == DeveloperWorkStatus.Completed)
        {
            return BadRequest(new
            {
                message =
                    "Completed work cannot be edited."
            });
        }

        if (request.DueDateUtc.HasValue &&
            request.DueDateUtc.Value < DateTime.UtcNow.AddMinutes(-1))
        {
            return BadRequest(new
            {
                message =
                    "Due date cannot be in the past."
            });
        }

        work.Title = request.Title.Trim();

        work.Description =
            request.Description.Trim();

        work.WorkType =
            request.WorkType;

        work.Priority =
            request.Priority;

        work.DueDateUtc =
            request.DueDateUtc;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(MapWork(work));
    }
}