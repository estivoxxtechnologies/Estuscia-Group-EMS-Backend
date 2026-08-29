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

    public DailyWorkController(IAppDbContext context, ICurrentTenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] string? branch, [FromQuery] DateOnly? date)
    {
        var currentUserId = _tenantService.UserId;
        var query = _context.DailyWorkLogs
            .Include(d => d.User)
            .AsNoTracking();

        var isPrivileged = User.IsInRole("branchmanager") || User.IsInRole("companyadmin") || User.IsInRole("superadmin");
        if (!isPrivileged)
        {
            query = query.Where(d => d.UserId == currentUserId);
        }
        else if (!string.IsNullOrEmpty(branch) && branch != "All Branches")
        {
            query = query.Where(d => d.BranchName == branch);
        }

        if (date.HasValue)
        {
            query = query.Where(d => d.WorkDate == date.Value);
        }

        var logs = await query.OrderByDescending(d => d.CreatedAtUtc).Take(100).ToListAsync();
        return Ok(logs);
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitWorkLog([FromBody] SubmitWorkLogDto dto)
    {
        var currentUserId = _tenantService.UserId!.Value;
        var user = await _context.Users.FindAsync(currentUserId);
        if (user == null) return Unauthorized();

        var workLog = new DailyWorkLog
        {
            UserId = currentUserId,
            BranchName = user.BranchName,
            WorkDate = dto.WorkDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
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
