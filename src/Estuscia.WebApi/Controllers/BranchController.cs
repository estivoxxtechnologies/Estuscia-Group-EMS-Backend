using Estuscia.Application.Branches.DTOs;
using Estuscia.Application.Common.DTOs.Currency;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
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

    // =========================================================
    // GET CURRENT USER BRANCHES
    // =========================================================

    [HttpGet]
    public async Task<ActionResult<List<BranchDto>>> GetBranches(
        CancellationToken cancellationToken)
    {
        var tenantIdValue = User.FindFirst("tenant_id")?.Value;

        if (!int.TryParse(tenantIdValue, out var tenantId))
        {
            return Unauthorized(new
            {
                message =
                    "Tenant information is missing from the authenticated user."
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
                IsActive = b.IsActive,

                CurrencyId = b.CurrencyId,

                Currency = b.Currency == null
                    ? null
                    : new CurrencyDto(
                        b.Currency.Id,
                        b.Currency.Code,
                        b.Currency.Name,
                        b.Currency.Symbol,
                        b.Currency.IsActive
                    )
            })
            .ToListAsync(cancellationToken);

        return Ok(branches);
    }

    // =========================================================
    // GET BRANCHES FOR SELECTED TENANT
    // =========================================================

    [HttpGet("tenant/{tenantId:int}")]
    public async Task<ActionResult<List<BranchDto>>> GetBranchesByTenant(
        int tenantId,
        CancellationToken cancellationToken)
    {
        var tenantExists = await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(
                t => t.Id == tenantId,
                cancellationToken);

        if (!tenantExists)
        {
            return NotFound(new
            {
                message = "Tenant not found."
            });
        }

        var branches = await _dbContext.TenantBranches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .OrderBy(b => b.BranchName)
            .Select(b => new BranchDto
            {
                Id = b.Id,
                TenantId = b.TenantId,
                BranchName = b.BranchName,
                City = b.City,
                IsActive = b.IsActive,

                CurrencyId = b.CurrencyId,

                Currency = b.Currency == null
                    ? null
                    : new CurrencyDto(
                        b.Currency.Id,
                        b.Currency.Code,
                        b.Currency.Name,
                        b.Currency.Symbol,
                        b.Currency.IsActive
                    )
            })
            .ToListAsync(cancellationToken);

        return Ok(branches);
    }

    // =========================================================
    // CREATE BRANCH FOR TENANT
    // =========================================================

    [HttpPost("tenant/{tenantId:int}")]
    public async Task<ActionResult<BranchDto>> CreateBranch(
        int tenantId,
        [FromBody] CreateBranchDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.BranchName))
        {
            return BadRequest(new
            {
                message = "Branch name is required."
            });
        }

        // -----------------------------------------------------
        // TENANT
        // -----------------------------------------------------

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == tenantId,
                cancellationToken);

        if (tenant == null)
        {
            return NotFound(new
            {
                message = "Tenant not found."
            });
        }

        // -----------------------------------------------------
        // CURRENCY
        // -----------------------------------------------------

        var currency = await _dbContext.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c =>
                    c.Id == dto.CurrencyId &&
                    c.IsActive,
                cancellationToken);

        if (currency == null)
        {
            return BadRequest(new
            {
                message =
                    "Selected currency does not exist or is inactive."
            });
        }

        // -----------------------------------------------------
        // DUPLICATE BRANCH
        // -----------------------------------------------------

        var branchName = dto.BranchName.Trim();

        var duplicateExists =
            await _dbContext.TenantBranches.AnyAsync(
                b =>
                    b.TenantId == tenantId &&
                    b.BranchName == branchName,
                cancellationToken);

        if (duplicateExists)
        {
            return Conflict(new
            {
                message =
                    "A branch with this name already exists for this tenant."
            });
        }

        // -----------------------------------------------------
        // CREATE
        // -----------------------------------------------------

        var branch = new TenantBranch
        {
            TenantId = tenantId,
            BranchName = branchName,

            City = string.IsNullOrWhiteSpace(dto.City)
                ? null
                : dto.City.Trim(),

            IsActive = true,

            CurrencyId = dto.CurrencyId
        };

        _dbContext.TenantBranches.Add(branch);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        // -----------------------------------------------------
        // RETURN
        // -----------------------------------------------------

        return Ok(new BranchDto
        {
            Id = branch.Id,
            TenantId = branch.TenantId,
            BranchName = branch.BranchName,
            City = branch.City,
            IsActive = branch.IsActive,

            CurrencyId = currency.Id,

            Currency = new CurrencyDto(
                currency.Id,
                currency.Code,
                currency.Name,
                currency.Symbol,
                currency.IsActive
            )
        });
    }

    // =========================================================
    // UPDATE BRANCH
    // =========================================================

    [HttpPut("{branchId:int}")]
    public async Task<ActionResult<BranchDto>> UpdateBranch(
        int branchId,
        [FromBody] UpdateBranchDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.BranchName))
        {
            return BadRequest(new
            {
                message = "Branch name is required."
            });
        }

        // -----------------------------------------------------
        // BRANCH
        // -----------------------------------------------------

        var branch =
            await _dbContext.TenantBranches
                .FirstOrDefaultAsync(
                    b => b.Id == branchId,
                    cancellationToken);

        if (branch == null)
        {
            return NotFound(new
            {
                message = "Branch not found."
            });
        }

        // -----------------------------------------------------
        // CURRENCY
        // -----------------------------------------------------

        var currency = await _dbContext.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c =>
                    c.Id == dto.CurrencyId &&
                    c.IsActive,
                cancellationToken);

        if (currency == null)
        {
            return BadRequest(new
            {
                message =
                    "Selected currency does not exist or is inactive."
            });
        }

        // -----------------------------------------------------
        // DUPLICATE BRANCH
        // -----------------------------------------------------

        var branchName = dto.BranchName.Trim();

        var duplicateExists =
            await _dbContext.TenantBranches.AnyAsync(
                b =>
                    b.TenantId == branch.TenantId &&
                    b.Id != branchId &&
                    b.BranchName == branchName,
                cancellationToken);

        if (duplicateExists)
        {
            return Conflict(new
            {
                message =
                    "A branch with this name already exists for this tenant."
            });
        }

        // -----------------------------------------------------
        // UPDATE
        // -----------------------------------------------------

        branch.BranchName = branchName;

        branch.City =
            string.IsNullOrWhiteSpace(dto.City)
                ? null
                : dto.City.Trim();

        branch.IsActive = dto.IsActive;

        branch.CurrencyId = dto.CurrencyId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        // -----------------------------------------------------
        // RETURN
        // -----------------------------------------------------

        return Ok(new BranchDto
        {
            Id = branch.Id,
            TenantId = branch.TenantId,
            BranchName = branch.BranchName,
            City = branch.City,
            IsActive = branch.IsActive,

            CurrencyId = currency.Id,

            Currency = new CurrencyDto(
                currency.Id,
                currency.Code,
                currency.Name,
                currency.Symbol,
                currency.IsActive
            )
        });
    }

    // =========================================================
    // ENABLE / DISABLE BRANCH
    // =========================================================

    [HttpPatch("{branchId:int}/status")]
    public async Task<ActionResult<BranchDto>> ToggleBranchStatus(
        int branchId,
        [FromBody] bool isActive,
        CancellationToken cancellationToken)
    {
        var branch =
            await _dbContext.TenantBranches
                .FirstOrDefaultAsync(
                    b => b.Id == branchId,
                    cancellationToken);

        if (branch == null)
        {
            return NotFound(new
            {
                message = "Branch not found."
            });
        }

        branch.IsActive = isActive;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        var currency = await _dbContext.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == branch.CurrencyId,
                cancellationToken);

        return Ok(new BranchDto
        {
            Id = branch.Id,
            TenantId = branch.TenantId,
            BranchName = branch.BranchName,
            City = branch.City,
            IsActive = branch.IsActive,

            CurrencyId = branch.CurrencyId,

            Currency = currency == null
                ? null
                : new CurrencyDto(
                    currency.Id,
                    currency.Code,
                    currency.Name,
                    currency.Symbol,
                    currency.IsActive
                )
        });
    }
}