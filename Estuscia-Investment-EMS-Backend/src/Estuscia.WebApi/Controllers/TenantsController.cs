using Estuscia.Application.Common.DTOs;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "superadmin")]
public class TenantsController : ControllerBase
{
    private readonly IAppDbContext _context;

    public TenantsController(IAppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTenants()
    {
        var tenants = await _context.Tenants
            .Include(t => t.Branches)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return Ok(tenants);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantDto dto)
    {
        var tenant = new Tenant
        {
            Name = dto.Name,
            Code = dto.Code,
            Domain = dto.Domain,
            Plan = dto.Plan,
            Currency = dto.Currency,
            IsActive = true
        };

        foreach (var branch in dto.Branches)
        {
            tenant.Branches.Add(new TenantBranch { BranchName = branch });
        }

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        return Ok(tenant);
    }
}
