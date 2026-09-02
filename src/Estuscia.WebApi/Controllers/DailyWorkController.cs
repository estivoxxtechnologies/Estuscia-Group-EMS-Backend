using Estuscia.Application.Common.DTOs;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
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
    // GET WORK LOGS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] Guid? branchId,
        [FromQuery] DateOnly? date)
    {
        var currentUserId = _tenantService.UserId;

        var query = _context.DailyWorkLogs
            .Include(d => d.User)
            .Include(d => d.Branch)
            .AsNoTracking();

        var isPrivileged =
            User.IsInRole("branch_manager") ||
            User.IsInRole("company_admin") ||
            User.IsInRole("super_admin");

        if (!isPrivileged)
        {
            if (!currentUserId.HasValue)
                return Unauthorized();

            query = query.Where(d =>
                d.UserId == currentUserId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(d =>
                d.BranchId == branchId.Value);
        }

        if (date.HasValue)
        {
            query = query.Where(d =>
                d.WorkDate == date.Value);
        }

        var logs = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(100)
            .ToListAsync();

        return Ok(logs);
    }

    // ============================================================
    // SUBMIT WORK LOG
    // ============================================================

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitWorkLog(
        [FromBody] SubmitWorkLogDto dto)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        var user = await _context.Users
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (user == null)
            return Unauthorized();

        if (user.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Your account is not assigned to a branch."
            });
        }

        var workLog = new DailyWorkLog
        {
            TenantId = user.TenantId,
            BranchId = user.BranchId,

            UserId = currentUserId,

            WorkDate =
                dto.WorkDate ??
                DateOnly.FromDateTime(DateTime.UtcNow),

            WorkType = dto.WorkType,
            Narration = dto.Narration,

            CallsMade = dto.CallsMade,
            CallsConnected = dto.CallsConnected,
            LeadsRespondedWell = dto.LeadsRespondedWell,
            FollowUpsScheduled = dto.FollowUpsScheduled,

            HoursSpent = dto.HoursSpent,
            FeaturesShipped = dto.FeaturesShipped,
            RepositoryPrLinks = dto.RepositoryPrLinks,
            BlockersEncountered = dto.BlockersEncountered,

            Status = "Submitted"
        };

        _context.DailyWorkLogs.Add(workLog);

        await _context.SaveChangesAsync();

        return Ok(workLog);
    }
}