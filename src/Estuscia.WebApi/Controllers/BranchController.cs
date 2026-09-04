using Estuscia.Application.Branches.DTOs;
using Estuscia.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IAppDbContext _dbContext;

    public BranchesController(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<BranchDto>>> GetBranches(
        CancellationToken cancellationToken)
    {
        var tenantIdValue = User.FindFirst("tenant_id")?.Value;

        if (!int.TryParse(tenantIdValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message = "Tenant information is missing from the authenticated user."
            });
        }

        var branches = await _dbContext.TenantBranches
            .AsNoTracking()
            .Where(b =>
                b.TenantId == tenantId &&
                b.IsActive)
            .OrderBy(b => b.BranchName)
            .Select(b => new BranchDto
            {
                Id = b.Id,
                TenantId = b.TenantId,
                BranchName = b.BranchName,
                City = b.City,
                IsActive = b.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(branches);
    }
}