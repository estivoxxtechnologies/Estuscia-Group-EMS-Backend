using System.Security.Claims;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Estuscia.Infrastructure.Persistence;
using Estuscia.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalaryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _tenantService;

    public SalaryController(
        AppDbContext db,
        ICurrentTenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    // ============================================================
    // GET /api/Salary/employees
    //
    // Employee Salary main table
    //
    // branchId:
    //   null  -> all branches within current tenant
    //   value -> only that branch within current tenant
    //
    // IMPORTANT:
    //   Branch Manager is ALWAYS restricted to their own branch.
    //   They cannot use branchId to access another branch.
    // ============================================================

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] int? branchId)
    {
        if (!CanViewSalary())
            return Forbid();

        var tenantId = _tenantService.TenantId;

        if (!tenantId.HasValue)
        {
            return BadRequest(new
            {
                message = "A tenant must be selected."
            });
        }

        var query = _db.Users
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId.Value &&
                x.IsActive);

        // ========================================================
        // BRANCH MANAGER
        //
        // Branch Manager can ONLY see employees from their own
        // assigned branch.
        //
        // Ignore the branchId supplied by frontend.
        // ========================================================

        if (IsBranchManager())
        {
            var managerBranchId = await GetCurrentUserBranchId();

            if (!managerBranchId.HasValue)
            {
                return BadRequest(new
                {
                    message = "Branch Manager has no assigned branch."
                });
            }

            query = query.Where(x =>
                x.BranchId == managerBranchId.Value);
        }
        else if (IsCompanyAdmin() || IsHrOps())
        {
            // ====================================================
            // COMPANY ADMIN / HR OPS
            //
            // branchId == null
            //      -> All branches in current tenant
            //
            // branchId != null
            //      -> Only selected branch
            // ====================================================

            if (branchId.HasValue)
            {
                // First verify that the branch belongs to the
                // currently selected tenant.
                var branchExists = await _db.TenantBranches
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.TenantId == tenantId.Value &&
                        x.Id == branchId.Value);

                if (!branchExists)
                {
                    return BadRequest(new
                    {
                        message = "The selected branch does not belong to the current tenant."
                    });
                }

                query = query.Where(x =>
                    x.BranchId == branchId.Value);
            }
        }
        else if (IsSuperAdmin())
        {
            // ====================================================
            // SUPER ADMIN
            //
            // Tenant is still enforced above.
            //
            // branchId can optionally be used if the frontend
            // sends one.
            // ====================================================

            if (branchId.HasValue)
            {
                var branchExists = await _db.TenantBranches
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.TenantId == tenantId.Value &&
                        x.Id == branchId.Value);

                if (!branchExists)
                {
                    return BadRequest(new
                    {
                        message = "The selected branch does not belong to the current tenant."
                    });
                }

                query = query.Where(x =>
                    x.BranchId == branchId.Value);
            }
        }
        else
        {
            // ====================================================
            // NORMAL EMPLOYEE
            //
            // They should only see themselves.
            // ====================================================

            var currentUserId = GetCurrentUserId();

            if (!currentUserId.HasValue)
                return Unauthorized();

            query = query.Where(x =>
                x.Id == currentUserId.Value);
        }

        var employees = await query
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                UserId = x.Id,
                EmployeeCode = x.EmployeeCode,
                EmployeeName = x.FullName,
                Designation = x.Designation,
                Department = x.Department,
                BranchId = x.BranchId,
                CurrentSalary = x.SalaryBase
            })
            .ToListAsync();

        var userIds = employees
            .Select(x => x.UserId)
            .ToList();

        // ========================================================
        // GET PENDING SALARY CHANGES
        // ========================================================

        var pendingChanges = await _db.EmployeeSalaryHistories
            .AsNoTracking()
            .Where(x =>
                x.UserId != 0 &&
                userIds.Contains(x.UserId) &&
                x.Status == SalaryChangeStatus.PendingApproval)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        // ========================================================
        // BUILD RESULT
        // ========================================================

        var result = employees
            .Select(employee =>
            {
                var pending = pendingChanges
                    .FirstOrDefault(x =>
                        x.UserId == employee.UserId);

                return new SalaryEmployeeDto
                {
                    UserId = employee.UserId,

                    EmployeeCode = employee.EmployeeCode,

                    EmployeeName = employee.EmployeeName,

                    Designation = employee.Designation,

                    Department = employee.Department,

                    BranchId = employee.BranchId,

                    CurrentSalary = employee.CurrentSalary,

                    PendingSalary = pending?.NewSalary,

                    PendingHistoryId = pending?.Id,

                    PendingStatus =
                        pending?.Status.ToString()
                };
            })
            .ToList();

        return Ok(result);
    }

    // ============================================================
    // GET /api/Salary/{userId}/history
    //
    // Complete salary history for one employee
    // ============================================================

    [HttpGet("{userId:int}/history")]
    public async Task<IActionResult> GetSalaryHistory(int userId)
    {
        if (!CanViewSalary())
            return Forbid();

        var user = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.EmployeeCode,
                x.FullName,
                x.Designation,
                x.Department,
                x.BranchId,
                x.SalaryBase
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new
            {
                message = "Employee not found."
            });
        }

        if (!await CanAccessEmployee(user.Id, user.BranchId))
            return Forbid();

        var history = await _db.EmployeeSalaryHistories
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new SalaryHistoryDto
            {
                Id = x.Id,

                UserId = x.UserId,

                PreviousSalary = x.PreviousSalary,

                NewSalary = x.NewSalary,

                Reason = x.Reason,

                Status = x.Status.ToString(),

                CreatedAtUtc = x.CreatedAtUtc,

                CreatedByUserId = x.CreatedByUserId,

                ApprovedByUserId = x.ApprovedByUserId,

                ApprovedAtUtc = x.ApprovedAtUtc,

                RejectedByUserId = x.RejectedByUserId,

                RejectedAtUtc = x.RejectedAtUtc,

                RejectionReason = x.RejectionReason
            })
            .ToListAsync();

        return Ok(new SalaryHistoryResponseDto
        {
            UserId = user.Id,
            EmployeeCode = user.EmployeeCode,
            EmployeeName = user.FullName,
            Designation = user.Designation,
            Department = user.Department,
            CurrentSalary = user.SalaryBase,

            History = history
        });
    }

    // ============================================================
    // POST /api/Salary/propose
    //
    // HR / Branch Manager proposes salary change
    // ============================================================

    [HttpPost("propose")]
    public async Task<IActionResult> ProposeSalary(
        [FromBody] ProposeSalaryRequest request)
    {
        if (!CanManageSalary())
            return Forbid();

        if (request.UserId <= 0)
        {
            return BadRequest(new
            {
                message = "Employee is required."
            });
        }

        if (request.NewSalary <= 0)
        {
            return BadRequest(new
            {
                message = "New salary must be greater than zero."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new
            {
                message = "Salary change reason is required."
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(x =>
                x.Id == request.UserId &&
                x.IsActive);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Active employee not found."
            });
        }

        if (!await CanAccessEmployee(
                user.Id,
                user.BranchId))
        {
            return Forbid();
        }

        var existingPending = await _db.EmployeeSalaryHistories
            .FirstOrDefaultAsync(x =>
                x.UserId == user.Id &&
                x.Status == SalaryChangeStatus.PendingApproval);

        if (existingPending != null)
        {
            return Conflict(new
            {
                message =
                    "This employee already has a salary change waiting for Company Admin approval.",
                historyId = existingPending.Id,
                pendingSalary = existingPending.NewSalary
            });
        }

        // Do not create meaningless proposals.
        if (user.SalaryBase == request.NewSalary)
        {
            return BadRequest(new
            {
                message =
                    "The proposed salary is the same as the current salary."
            });
        }

        var currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
            return Unauthorized();

        var history = new EmployeeSalaryHistory
        {
            TenantId = user.TenantId,

            UserId = user.Id,

            BranchId = user.BranchId,

            PreviousSalary = user.SalaryBase,

            NewSalary = request.NewSalary,

            Reason = request.Reason.Trim(),

            Status = SalaryChangeStatus.PendingApproval,

            CreatedByUserId = currentUserId.Value,

            CreatedAtUtc = DateTime.UtcNow
        };

        _db.EmployeeSalaryHistories.Add(history);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Salary change submitted for Company Admin approval.",

            historyId = history.Id,

            userId = user.Id,

            previousSalary = history.PreviousSalary,

            newSalary = history.NewSalary,

            status = history.Status.ToString()
        });
    }

    // ============================================================
    // POST /api/Salary/{historyId}/approve
    //
    // ONLY Company Admin / SuperAdmin
    //
    // This is where Users.SalaryBase changes.
    // ============================================================

    [HttpPost("{historyId:int}/approve")]
    public async Task<IActionResult> ApproveSalaryChange(
        int historyId)
    {
        if (!IsCompanyAdmin() && !IsSuperAdmin())
            return Forbid();

        var history = await _db.EmployeeSalaryHistories
            .FirstOrDefaultAsync(x => x.Id == historyId);

        if (history == null)
        {
            return NotFound(new
            {
                message = "Salary change request not found."
            });
        }

        if (history.Status != SalaryChangeStatus.PendingApproval)
        {
            return BadRequest(new
            {
                message =
                    $"Salary change is already {history.Status}."
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(x =>
                x.Id == history.UserId);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Employee not found."
            });
        }

        if (user.TenantId != history.TenantId)
        {
            return BadRequest(new
            {
                message = "Employee and salary request tenant do not match."
            });
        }

        // IMPORTANT:
        //
        // The salary stored in Users must still match
        // the salary that existed when the proposal was created.
        //
        // This prevents approving an outdated proposal
        // after another salary change has already occurred.

        if (user.SalaryBase != history.PreviousSalary)
        {
            return Conflict(new
            {
                message =
                    "The employee's current salary has changed since this proposal was created. This salary request cannot be approved. Create a new proposal."
            });
        }

        var currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
            return Unauthorized();

        // ========================================================
        // APPROVAL
        //
        // Only here do we update Users.SalaryBase.
        // ========================================================

        user.SalaryBase = history.NewSalary;

        history.Status = SalaryChangeStatus.Approved;

        history.ApprovedByUserId = currentUserId.Value;

        history.ApprovedAtUtc = DateTime.UtcNow;

        history.UpdatedByUserId = currentUserId.Value;

        history.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Salary change approved. Employee's current salary has been updated.",

            historyId = history.Id,

            userId = user.Id,

            previousSalary = history.PreviousSalary,

            newSalary = history.NewSalary,

            status = history.Status.ToString()
        });
    }

    // ============================================================
    // POST /api/Salary/{historyId}/reject
    //
    // Company Admin rejects proposal.
    //
    // Users.SalaryBase remains unchanged.
    // ============================================================

    [HttpPost("{historyId:int}/reject")]
    public async Task<IActionResult> RejectSalaryChange(
        int historyId,
        [FromBody] RejectSalaryRequest request)
    {
        if (!IsCompanyAdmin() && !IsSuperAdmin())
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new
            {
                message =
                    "Rejection reason is required."
            });
        }

        var history = await _db.EmployeeSalaryHistories
            .FirstOrDefaultAsync(x => x.Id == historyId);

        if (history == null)
        {
            return NotFound(new
            {
                message = "Salary change request not found."
            });
        }

        if (history.Status != SalaryChangeStatus.PendingApproval)
        {
            return BadRequest(new
            {
                message =
                    $"Salary change is already {history.Status}."
            });
        }

        var currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
            return Unauthorized();

        history.Status = SalaryChangeStatus.Rejected;

        history.RejectedByUserId = currentUserId.Value;

        history.RejectedAtUtc = DateTime.UtcNow;

        history.RejectionReason =
            request.Reason.Trim();

        history.UpdatedByUserId = currentUserId.Value;

        history.UpdatedAtUtc = DateTime.UtcNow;

        // IMPORTANT:
        //
        // We deliberately DO NOT update:
        //
        // user.SalaryBase
        //
        // because rejected salary changes must never affect
        // the employee's current salary.

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Salary change rejected. Employee's current salary remains unchanged.",

            historyId = history.Id,

            status = history.Status.ToString()
        });
    }

    // ============================================================
    // GET /api/Salary/pending
    //
    // Company Admin salary approval screen
    // ============================================================

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingSalaryChanges()
    {
        if (!IsCompanyAdmin() && !IsSuperAdmin())
            return Forbid();

        var query = _db.EmployeeSalaryHistories
            .AsNoTracking()
            .Where(x =>
                x.Status == SalaryChangeStatus.PendingApproval)
            .AsQueryable();

        var requests = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new PendingSalaryChangeDto
            {
                Id = x.Id,

                UserId = x.UserId,

                EmployeeCode = x.User.EmployeeCode,

                EmployeeName = x.User.FullName,

                Designation = x.User.Designation,

                Department = x.User.Department,

                BranchId = x.BranchId,

                PreviousSalary = x.PreviousSalary,

                NewSalary = x.NewSalary,

                Reason = x.Reason,

                Status = x.Status.ToString(),

                CreatedAtUtc = x.CreatedAtUtc,

                CreatedByUserId = x.CreatedByUserId
            })
            .ToListAsync();

        return Ok(requests);
    }

    // ============================================================
    // PRIVATE: Employee access
    // ============================================================

    private async Task<bool> CanAccessEmployee(
        int userId,
        int? branchId)
    {
        if (IsSuperAdmin() ||
            IsCompanyAdmin() ||
            IsHrOps())
        {
            return true;
        }

        if (IsBranchManager())
        {
            var managerBranchId =
                await GetCurrentUserBranchId();

            return managerBranchId.HasValue &&
                   branchId == managerBranchId.Value;
        }

        var currentUserId = GetCurrentUserId();

        return currentUserId.HasValue &&
               currentUserId.Value == userId;
    }

    // ============================================================
    // PRIVATE: Permissions
    // ============================================================

    private bool CanViewSalary()
    {
        return IsSuperAdmin()
            || IsCompanyAdmin()
            || IsHrOps()
            || IsBranchManager()
            || IsEmployee();
    }

    private bool CanManageSalary()
    {
        return IsSuperAdmin()
            || IsHrOps()
            || IsBranchManager();
    }

    private bool IsEmployee()
    {
        return HasRole(
            "sales_staff",
            "developer",
            "support_staff",
            "knowledge_trainer");
    }

    private bool IsBranchManager()
    {
        return HasRole("branch_manager");
    }

    private bool IsHrOps()
    {
        return HasRole("hr_ops");
    }

    private bool IsCompanyAdmin()
    {
        return HasRole("company_admin");
    }

    private bool IsSuperAdmin()
    {
        return HasRole("super_admin");
    }

    // ============================================================
    // PRIVATE: Authentication
    // ============================================================

    private bool HasRole(params string[] roles)
    {
        var role =
            User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? User.FindFirstValue("Role");

        if (string.IsNullOrWhiteSpace(role))
            return false;

        return roles.Any(x =>
            string.Equals(
                x,
                role,
                StringComparison.OrdinalIgnoreCase));
    }

    private int? GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("UserId");

        if (int.TryParse(value, out var id))
            return id;

        return null;
    }

    private async Task<int?> GetCurrentUserBranchId()
    {
        var userId = GetCurrentUserId();

        if (!userId.HasValue)
            return null;

        return await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => x.BranchId)
            .FirstOrDefaultAsync();
    }
}


// ==================================================================
// REQUEST DTOs
// ==================================================================

public class ProposeSalaryRequest
{
    public int UserId { get; set; }

    public decimal NewSalary { get; set; }

    public string Reason { get; set; } = string.Empty;
}


public class RejectSalaryRequest
{
    public string Reason { get; set; } = string.Empty;
}


// ==================================================================
// RESPONSE DTOs
// ==================================================================

public class SalaryEmployeeDto
{
    public int UserId { get; set; }

    public string? EmployeeCode { get; set; }

    public string? EmployeeName { get; set; }

    public string? Designation { get; set; }

    public string? Department { get; set; }

    public int? BranchId { get; set; }

    public decimal CurrentSalary { get; set; }

    public decimal? PendingSalary { get; set; }

    public int? PendingHistoryId { get; set; }

    public string? PendingStatus { get; set; }
}


public class SalaryHistoryResponseDto
{
    public int UserId { get; set; }

    public string? EmployeeCode { get; set; }

    public string? EmployeeName { get; set; }

    public string? Designation { get; set; }

    public string? Department { get; set; }

    public decimal CurrentSalary { get; set; }

    public List<SalaryHistoryDto> History { get; set; }
        = new();
}


public class SalaryHistoryDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public decimal PreviousSalary { get; set; }

    public decimal NewSalary { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public int? CreatedByUserId { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public int? RejectedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }
}


public class PendingSalaryChangeDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? EmployeeCode { get; set; }

    public string? EmployeeName { get; set; }

    public string? Designation { get; set; }

    public string? Department { get; set; }

    public int? BranchId { get; set; }

    public decimal PreviousSalary { get; set; }

    public decimal NewSalary { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public int? CreatedByUserId { get; set; }
}