
using System.Security.Claims;
using Estuscia.Domain.Entities;
using Estuscia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchWorkingDaysController : ControllerBase
{
    private readonly AppDbContext _db;

    public BranchWorkingDaysController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int branchId,
        [FromQuery] int? tenantId,
        CancellationToken ct)
    {
        var user = await GetCurrentUser(ct);

        if (user == null)
            return Unauthorized();

        if (!IsManagementRole(user.RoleNumber))
            return Forbid();

        if (branchId <= 0)
            return BadRequest("A valid branchId is required.");

        var effectiveTenantId =
            user.RoleNumber == 1 ? tenantId : user.TenantId;

        if (!effectiveTenantId.HasValue)
            return BadRequest("A tenantId is required.");

        if (user.RoleNumber != 1 &&
            user.TenantId != effectiveTenantId)
            return Forbid();

        if (user.RoleNumber == 4 &&
            user.BranchId != branchId)
            return Forbid();

        var branchExists = await _db.TenantBranches
            .AnyAsync(b =>
                b.Id == branchId &&
                b.TenantId == effectiveTenantId.Value &&
                b.IsActive, ct);

        if (!branchExists)
            return NotFound("Branch not found or inactive.");

        var days = await _db.BranchWorkingDays
            .AsNoTracking()
            .Where(x =>
                x.TenantId == effectiveTenantId.Value &&
                x.BranchId == branchId)
            .OrderBy(x => x.DayOfWeek)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.BranchId,
                DayOfWeek = (int)x.DayOfWeek,
                x.IsWorkingDay
            })
            .ToListAsync(ct);

        // An empty result means no schedule has been saved yet.
        return Ok(days);
    }

    [HttpPut("{branchId:int}")]
    public async Task<IActionResult> Save(
        int branchId,
        [FromBody] SaveBranchWorkingDaysRequest request,
        [FromQuery] int? tenantId,
        CancellationToken ct)
    {
        var user = await GetCurrentUser(ct);

        if (user == null)
            return Unauthorized();

        if (!IsManagementRole(user.RoleNumber))
            return Forbid();

        if (branchId <= 0)
            return BadRequest("A valid branchId is required.");

        var effectiveTenantId =
            user.RoleNumber == 1
                ? request.TenantId ?? tenantId
                : user.TenantId;

        if (!effectiveTenantId.HasValue)
            return BadRequest("A tenantId is required.");

        if (user.RoleNumber != 1 &&
            user.TenantId != effectiveTenantId)
            return Forbid();

        if (user.RoleNumber == 4 &&
            user.BranchId != branchId)
            return Forbid();

        if (request.Days == null ||
            request.Days.Count != 7 ||
            request.Days.Select(x => x.DayOfWeek).Distinct().Count() != 7 ||
            request.Days.Any(x =>
                x.DayOfWeek < 0 || x.DayOfWeek > 6))
        {
            return BadRequest(
                "Provide exactly one entry for each weekday, Sunday (0) through Saturday (6).");
        }

        var branchExists = await _db.TenantBranches
            .AnyAsync(b =>
                b.Id == branchId &&
                b.TenantId == effectiveTenantId.Value &&
                b.IsActive, ct);

        if (!branchExists)
            return NotFound("Branch not found or inactive.");

        var existing = await _db.BranchWorkingDays
            .Where(x =>
                x.TenantId == effectiveTenantId.Value &&
                x.BranchId == branchId)
            .ToListAsync(ct);

        foreach (var item in request.Days)
        {
            var day = existing.FirstOrDefault(x =>
                (int)x.DayOfWeek == item.DayOfWeek);

            if (day == null)
            {
                _db.BranchWorkingDays.Add(new BranchWorkingDay
                {
                    TenantId = effectiveTenantId.Value,
                    BranchId = branchId,
                    DayOfWeek = (DayOfWeek)item.DayOfWeek,
                    IsWorkingDay = item.IsWorkingDay
                });
            }
            else
            {
                day.IsWorkingDay = item.IsWorkingDay;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Branch working schedule saved successfully."
        });
    }

    private async Task<CurrentUserInfo?> GetCurrentUser(
        CancellationToken ct)
    {
        var idValue = User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (!int.TryParse(idValue, out var id))
            return null;

        return await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => new CurrentUserInfo
            {
                TenantId = x.TenantId,
                BranchId = x.BranchId,
                RoleNumber = x.RoleNumber
            })
            .FirstOrDefaultAsync(ct);
    }

    private static bool IsManagementRole(int roleNumber) =>
        roleNumber is 1 or 2 or 3 or 4;

    private sealed class CurrentUserInfo
    {
        public int? TenantId { get; set; }
        public int? BranchId { get; set; }
        public int RoleNumber { get; set; }
    }
}

public sealed class SaveBranchWorkingDaysRequest
{
    public int? TenantId { get; set; }

    public List<BranchWorkingDayItem> Days { get; set; } = [];
}

public sealed class BranchWorkingDayItem
{
    public int DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
}
