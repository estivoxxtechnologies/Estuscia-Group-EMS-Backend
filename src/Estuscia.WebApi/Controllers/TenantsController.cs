using Estuscia.Application.Common.DTOs.Tenant;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "super_admin")]
public class TenantsController : ControllerBase
{
    private readonly IAppDbContext _context;

    public TenantsController(IAppDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET ALL TENANTS
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> GetAllTenants(
        CancellationToken cancellationToken)
    {
        var tenants = await _context.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                id = t.Id,
                name = t.Name,
                code = t.Code,
                domain = t.Domain,
                plan = t.Plan,
                currency = t.Currency,
                isActive = t.IsActive,
                createdAtUtc = t.CreatedAtUtc,
                updatedAtUtc = t.UpdatedAtUtc,
                createdByUserId = t.CreatedByUserId
            })
            .ToListAsync(cancellationToken);

        return Ok(tenants);
    }

    // =========================================================
    // CREATE TENANT
    // =========================================================

    [HttpPost]
    public async Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("Tenant name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest("Tenant code is required.");
        }

        var code = dto.Code.Trim();

        var codeExists = await _context.Tenants
            .AnyAsync(
                t => t.Code == code,
                cancellationToken);

        if (codeExists)
        {
            return Conflict("A tenant with this code already exists.");
        }

        var tenant = new Tenant
        {
            Name = dto.Name.Trim(),
            Code = code,
            Domain = dto.Domain?.Trim() ?? string.Empty,
            Plan = dto.Plan?.Trim() ?? "Enterprise Pro",
            Currency = dto.Currency?.Trim() ?? "INR",
            IsActive = true
        };

        _context.Tenants.Add(tenant);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = tenant.Id,
            name = tenant.Name,
            code = tenant.Code,
            domain = tenant.Domain,
            plan = tenant.Plan,
            currency = tenant.Currency,
            isActive = tenant.IsActive,
            createdAtUtc = tenant.CreatedAtUtc,
            updatedAtUtc = tenant.UpdatedAtUtc,
            createdByUserId = tenant.CreatedByUserId
        });
    }

    // =========================================================
    // UPDATE TENANT
    // =========================================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTenant(
        int id,
        [FromBody] UpdateTenantDto dto,
        CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(
                t => t.Id == id,
                cancellationToken);

        if (tenant == null)
        {
            return NotFound("Tenant not found.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("Tenant name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest("Tenant code is required.");
        }

        var code = dto.Code.Trim();

        var codeExists = await _context.Tenants
            .AnyAsync(
                t => t.Code == code && t.Id != id,
                cancellationToken);

        if (codeExists)
        {
            return Conflict("A tenant with this code already exists.");
        }

        tenant.Name = dto.Name.Trim();
        tenant.Code = code;
        tenant.Domain = dto.Domain?.Trim() ?? string.Empty;
        tenant.Plan = dto.Plan?.Trim() ?? "Enterprise Pro";
        tenant.Currency = dto.Currency?.Trim() ?? "INR";
        tenant.IsActive = dto.isActive;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = tenant.Id,
            name = tenant.Name,
            code = tenant.Code,
            domain = tenant.Domain,
            plan = tenant.Plan,
            currency = tenant.Currency,
            isActive = tenant.IsActive,
            createdAtUtc = tenant.CreatedAtUtc,
            updatedAtUtc = tenant.UpdatedAtUtc,
            createdByUserId = tenant.CreatedByUserId
        });
    }
}