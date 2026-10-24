
using Estuscia.Domain.Entities;
using Estuscia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompanyHolidaysController : ControllerBase
{
    private readonly AppDbContext _db;

    public CompanyHolidaysController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetHolidays(
        [FromQuery] int? tenantId,
        [FromQuery] int? branchId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUser(cancellationToken);

        if (user == null)
            return Unauthorized();

        if (!IsManagementRole(user.RoleNumber))
            return Forbid();

        if (fromDate.HasValue && toDate.HasValue &&
            fromDate.Value > toDate.Value)
        {
            return BadRequest("fromDate cannot be later than toDate.");
        }

        var effectiveTenantId =
            user.RoleNumber == 1 ? tenantId : user.TenantId;

        if (!effectiveTenantId.HasValue)
            return BadRequest("A tenantId is required.");

        if (user.RoleNumber != 1 &&
            user.TenantId != effectiveTenantId)
        {
            return Forbid();
        }

        IQueryable<CompanyHoliday> query = _db.CompanyHolidays
            .AsNoTracking()
            .Where(x => x.TenantId == effectiveTenantId.Value);

        // A branch manager sees their own branch's holidays
        // AND company-wide holidays.
        if (user.RoleNumber == 4)
        {
            if (!user.BranchId.HasValue)
                return BadRequest(
                    "Branch manager is not assigned to a branch.");

            var managerBranchId = user.BranchId.Value;

            query = query.Where(x =>
                x.BranchId == null ||
                x.BranchId == managerBranchId);
        }
        // For other roles, a branch filter includes global holidays.
        else if (branchId.HasValue)
        {
            query = query.Where(x =>
                x.BranchId == null ||
                x.BranchId == branchId.Value);
        }

        if (fromDate.HasValue)
            query = query.Where(x => x.Date >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.Date <= toDate.Value);

        var holidays = await query
            .OrderBy(x => x.Date)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.BranchId,
                BranchName = x.Branch != null
                    ? x.Branch.BranchName
                    : null,
                x.Name,
                x.Date,
                x.Description,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.CreatedByUserId,
                x.UpdatedByUserId
            })
            .ToListAsync(cancellationToken);

        return Ok(holidays);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetHolidayById(
        int id,
        [FromQuery] int? tenantId,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUser(cancellationToken);

        if (user == null)
            return Unauthorized();

        if (!IsManagementRole(user.RoleNumber))
            return Forbid();

        var effectiveTenantId =
            user.RoleNumber == 1 ? tenantId : user.TenantId;

        if (!effectiveTenantId.HasValue)
            return BadRequest("A tenantId is required.");

        if (user.RoleNumber != 1 &&
            user.TenantId != effectiveTenantId)
        {
            return Forbid();
        }

        var query = _db.CompanyHolidays
            .AsNoTracking()
            .Where(x =>
                x.Id == id &&
                x.TenantId == effectiveTenantId.Value);

        if (user.RoleNumber == 4)
        {
            if (!user.BranchId.HasValue)
                return BadRequest(
                    "Branch manager is not assigned to a branch.");

            var managerBranchId = user.BranchId.Value;

            query = query.Where(x =>
                x.BranchId == null ||
                x.BranchId == managerBranchId);
        }

        var holiday = await query
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.BranchId,
                BranchName = x.Branch != null
                    ? x.Branch.BranchName
                    : null,
                x.Name,
                x.Date,
                x.Description,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.CreatedByUserId,
                x.UpdatedByUserId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return holiday == null
            ? NotFound("Company holiday not found.")
            : Ok(holiday);
    }

    [HttpPost]
    public async Task<IActionResult> CreateHoliday(
        [FromBody] CompanyHolidayRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUser(cancellationToken);

        if (user == null)
            return Unauthorized();

        if (!CanManageHolidays(user.RoleNumber))
            return Forbid();

        var validationError = ValidateRequest(request);

        if (validationError != null)
            return BadRequest(validationError);

        var effectiveTenantId =
            user.RoleNumber == 1 ? request.TenantId : user.TenantId;

        if (!effectiveTenantId.HasValue)
            return BadRequest("A tenantId is required.");

        if (user.RoleNumber != 1 &&
            user.TenantId != effectiveTenantId)
        {
            return Forbid();
        }

        var branchId = request.BranchId;

        // Branch managers cannot create company-wide holidays
        // or create holidays for another branch.
        if (user.RoleNumber == 4)
        {
            if (!user.BranchId.HasValue)
                return BadRequest(
                    "Branch manager is not assigned to a branch.");

            if (!branchId.HasValue ||
                branchId.Value != user.BranchId.Value)
            {
                return Forbid();
            }
        }

        if (branchId.HasValue)
        {
            var branchExists = await _db.TenantBranches
                .AnyAsync(x =>
                    x.Id == branchId.Value &&
                    x.TenantId == effectiveTenantId.Value &&
                    x.IsActive,
                    cancellationToken);

            if (!branchExists)
            {
                return BadRequest(
                    "Branch does not exist, is inactive, or belongs to another tenant.");
            }
        }

        // Prevent duplicate dates for the same branch.
        // When BranchId is null, check only other company-wide holidays.
        var duplicate = await _db.CompanyHolidays
            .AnyAsync(x =>
                x.TenantId == effectiveTenantId.Value &&
                x.BranchId == branchId &&
                x.Date == request.Date,
                cancellationToken);

        if (duplicate)
        {
            return Conflict(
                "A holiday already exists for this branch and date.");
        }

        var now = DateTime.UtcNow;

        var holiday = new CompanyHoliday
        {
            TenantId = effectiveTenantId.Value,
            BranchId = branchId,
            Name = request.Name.Trim(),
            Date = request.Date,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            CreatedAtUtc = now,
            CreatedByUserId = user.Id
        };

        _db.CompanyHolidays.Add(holiday);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(
                "The holiday could not be saved. Check for a duplicate holiday or invalid database relationship.");
        }

        return CreatedAtAction(
            nameof(GetHolidayById),
            new
            {
                id = holiday.Id,
                tenantId = effectiveTenantId.Value
            },
            new
            {
                holiday.Id,
                holiday.TenantId,
                holiday.BranchId,
                BranchName = (string?)null,
                holiday.Name,
                holiday.Date,
                holiday.Description
            });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateHoliday(
        int id,
        [FromBody] CompanyHolidayRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUser(cancellationToken);

        if (user == null)
            return Unauthorized();

        if (!CanManageHolidays(user.RoleNumber))
            return Forbid();

        var validationError = ValidateRequest(request);

        if (validationError != null)
            return BadRequest(validationError);

        var effectiveTenantId =
            user.RoleNumber == 1 ? request.TenantId : user.TenantId;

        if (!effectiveTenantId.HasValue)
            return BadRequest("A tenantId is required.");

        if (user.RoleNumber != 1 &&
            user.TenantId != effectiveTenantId)
        {
            return Forbid();
        }

        var holiday = await _db.CompanyHolidays
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == effectiveTenantId.Value,
                cancellationToken);

        if (holiday == null)
            return NotFound("Company holiday not found.");

        // Branch managers may update only their own branch's holidays.
        // They cannot edit a company-wide holiday.
        if (user.RoleNumber == 4)
        {
            if (!user.BranchId.HasValue)
                return BadRequest(
                    "Branch manager is not assigned to a branch.");

            if (holiday.BranchId != user.BranchId.Value ||
                request.BranchId != user.BranchId.Value)
            {
                return Forbid();
            }
        }

        var branchId = request.BranchId;

        if (branchId.HasValue)
        {
            var branchExists = await _db.TenantBranches
                .AnyAsync(x =>
                    x.Id == branchId.Value &&
                    x.TenantId == effectiveTenantId.Value &&
                    x.IsActive,
                    cancellationToken);

            if (!branchExists)
            {
                return BadRequest(
                    "Branch does not exist, is inactive, or belongs to another tenant.");
            }
        }

        var duplicate = await _db.CompanyHolidays
            .AnyAsync(x =>
                x.Id != id &&
                x.TenantId == effectiveTenantId.Value &&
                x.BranchId == branchId &&
                x.Date == request.Date,
                cancellationToken);

        if (duplicate)
        {
            return Conflict(
                "A holiday already exists for this branch and date.");
        }

        holiday.BranchId = branchId;
        holiday.Name = request.Name.Trim();
        holiday.Date = request.Date;
        holiday.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        holiday.UpdatedAtUtc = DateTime.UtcNow;
        holiday.UpdatedByUserId = user.Id;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(
                "The holiday could not be updated. Check for a duplicate holiday or invalid database relationship.");
        }

        return Ok(new
        {
            message = "Company holiday updated successfully.",
            holiday.Id,
            holiday.TenantId,
            holiday.BranchId,
            holiday.Name,
            holiday.Date,
            holiday.Description
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteHoliday(
        int id,
        [FromQuery] int? tenantId,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUser(cancellationToken);

        if (user == null)
            return Unauthorized();

        if (!CanManageHolidays(user.RoleNumber))
            return Forbid();

        var effectiveTenantId =
            user.RoleNumber == 1 ? tenantId : user.TenantId;

        if (!effectiveTenantId.HasValue)
            return BadRequest("A tenantId is required.");

        if (user.RoleNumber != 1 &&
            user.TenantId != effectiveTenantId)
        {
            return Forbid();
        }

        var query = _db.CompanyHolidays.Where(x =>
            x.Id == id &&
            x.TenantId == effectiveTenantId.Value);

        if (user.RoleNumber == 4)
        {
            if (!user.BranchId.HasValue)
                return BadRequest(
                    "Branch manager is not assigned to a branch.");

            query = query.Where(x =>
                x.BranchId == user.BranchId.Value);
        }

        var holiday = await query
            .FirstOrDefaultAsync(cancellationToken);

        if (holiday == null)
            return NotFound("Company holiday not found.");

        _db.CompanyHolidays.Remove(holiday);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Company holiday deleted successfully."
        });
    }

    private static string? ValidateRequest(
        CompanyHolidayRequest request)
    {
        if (request == null)
            return "A request body is required.";

        if (string.IsNullOrWhiteSpace(request.Name))
            return "Holiday name is required.";

        if (request.Name.Trim().Length > 200)
            return "Holiday name cannot exceed 200 characters.";

        if (request.Description?.Length > 1000)
            return "Description cannot exceed 1000 characters.";

        return null;
    }

    private async Task<CurrentUserInfo?> GetCurrentUser(
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            userIdClaim = User.FindFirst("sub")?.Value;

            if (!int.TryParse(userIdClaim, out userId))
                return null;
        }

        return await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new CurrentUserInfo
            {
                Id = x.Id,
                TenantId = x.TenantId,
                BranchId = x.BranchId,
                RoleNumber = x.RoleNumber,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken) is
        { IsActive: true } user
                    ? user
                    : null;
    }

    private static bool IsManagementRole(int roleNumber) =>
        roleNumber is 1 or 2 or 3 or 4;

    private static bool CanManageHolidays(int roleNumber) =>
        roleNumber is 1 or 2 or 3 or 4;

    private sealed class CurrentUserInfo
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int? BranchId { get; set; }
        public int RoleNumber { get; set; }
        public bool IsActive { get; set; }
    }
}

public class CompanyHolidayRequest
{
    public int? TenantId { get; set; }
    public int? BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
}
