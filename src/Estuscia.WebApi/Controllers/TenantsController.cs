using Estuscia.Application.Common.DTOs.Tenant;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
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
            .Include(t => t.DefaultCurrency)
            .OrderBy(t => t.Name)
            .Select(t => new
            {
                id = t.Id,
                name = t.Name,
                code = t.Code,
                domain = t.Domain,
                plan = t.Plan,

                // -------------------------------------------------
                // CURRENCY
                // -------------------------------------------------

                defaultCurrencyId = t.DefaultCurrencyId,
                currency = t.DefaultCurrency.Code,
                currencyName = t.DefaultCurrency.Name,
                currencySymbol = t.DefaultCurrency.Symbol,

                // -------------------------------------------------
                // WORKING SCHEDULE
                // -------------------------------------------------

                standardWorkingHours = t.StandardWorkingHours,
                workStartTime = t.WorkStartTime,
                workEndTime = t.WorkEndTime,

                // -------------------------------------------------
                // STATUS / AUDIT
                // -------------------------------------------------

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
        // ---------------------------------------------------------
        // BASIC VALIDATION
        // ---------------------------------------------------------

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("Tenant name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest("Tenant code is required.");
        }

        // ---------------------------------------------------------
        // STANDARD WORKING HOURS VALIDATION
        // ---------------------------------------------------------

        if (dto.StandardWorkingHours <= 0 ||
            dto.StandardWorkingHours > 24)
        {
            return BadRequest(
                "Standard working hours must be greater than 0 and cannot exceed 24 hours.");
        }

        // ---------------------------------------------------------
        // WORKING TIME VALIDATION
        // ---------------------------------------------------------

        if (dto.WorkStartTime >= dto.WorkEndTime)
        {
            return BadRequest(
                "Work start time must be earlier than work end time.");
        }

        var code = dto.Code.Trim();

        // ---------------------------------------------------------
        // CHECK DUPLICATE CODE
        // ---------------------------------------------------------

        var codeExists = await _context.Tenants
            .AnyAsync(
                t => t.Code == code,
                cancellationToken);

        if (codeExists)
        {
            return Conflict(
                "A tenant with this code already exists.");
        }

        // ---------------------------------------------------------
        // VALIDATE DEFAULT CURRENCY
        // ---------------------------------------------------------

        var currency = await _context.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == dto.DefaultCurrencyId &&
                     c.IsActive,
                cancellationToken);

        if (currency == null)
        {
            return BadRequest(
                "Selected default currency is invalid or inactive.");
        }

        // ---------------------------------------------------------
        // CREATE TENANT
        // ---------------------------------------------------------

        var tenant = new Tenant
        {
            Name = dto.Name.Trim(),
            Code = code,
            Domain = dto.Domain?.Trim() ?? string.Empty,
            Plan = dto.Plan?.Trim() ?? "Enterprise Pro",

            // -----------------------------------------------------
            // WORKING SCHEDULE
            // -----------------------------------------------------

            StandardWorkingHours = dto.StandardWorkingHours,
            WorkStartTime = dto.WorkStartTime,
            WorkEndTime = dto.WorkEndTime,

            // -----------------------------------------------------
            // CURRENCY
            // -----------------------------------------------------

            DefaultCurrencyId = currency.Id,

            // -----------------------------------------------------
            // STATUS
            // -----------------------------------------------------

            IsActive = dto.isActive
        };

        _context.Tenants.Add(tenant);

        // First save generates Tenant.Id
        await _context.SaveChangesAsync(cancellationToken);

        // ---------------------------------------------------------
        // CREATE INITIAL PAYMENT RECORD
        // ---------------------------------------------------------

        var tenantPayment = new TenantPayment
        {
            TenantId = tenant.Id,

            CurrencyId = currency.Id,

            TotalBranches = 0,

            PaymentMode = PaymentMode.Monthly,

            Amount = 0,

            PaymentStatus = PaymentStatus.Pending,

            PaymentDateUtc = null,
            ValidFromUtc = null,
            ValidUntilUtc = null,

            RegistrationStatus = false,

            Notes = null
        };

        _context.TenantPayments.Add(tenantPayment);

        await _context.SaveChangesAsync(cancellationToken);

        // ---------------------------------------------------------
        // RETURN CREATED TENANT
        // ---------------------------------------------------------

        return Ok(new
        {
            id = tenant.Id,
            name = tenant.Name,
            code = tenant.Code,
            domain = tenant.Domain,
            plan = tenant.Plan,

            // Currency
            defaultCurrencyId = tenant.DefaultCurrencyId,
            currency = currency.Code,
            currencyName = currency.Name,
            currencySymbol = currency.Symbol,

            // Working schedule
            standardWorkingHours = tenant.StandardWorkingHours,
            workStartTime = tenant.WorkStartTime,
            workEndTime = tenant.WorkEndTime,

            // Status / audit
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
        // ---------------------------------------------------------
        // FIND TENANT
        // ---------------------------------------------------------

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(
                t => t.Id == id,
                cancellationToken);

        if (tenant == null)
        {
            return NotFound("Tenant not found.");
        }

        // ---------------------------------------------------------
        // BASIC VALIDATION
        // ---------------------------------------------------------

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest("Tenant name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest("Tenant code is required.");
        }

        // ---------------------------------------------------------
        // STANDARD WORKING HOURS VALIDATION
        // ---------------------------------------------------------

        if (dto.StandardWorkingHours <= 0 ||
            dto.StandardWorkingHours > 24)
        {
            return BadRequest(
                "Standard working hours must be greater than 0 and cannot exceed 24 hours.");
        }

        // ---------------------------------------------------------
        // WORKING TIME VALIDATION
        // ---------------------------------------------------------

        if (dto.WorkStartTime >= dto.WorkEndTime)
        {
            return BadRequest(
                "Work start time must be earlier than work end time.");
        }

        var code = dto.Code.Trim();

        // ---------------------------------------------------------
        // CHECK DUPLICATE CODE
        // ---------------------------------------------------------

        var codeExists = await _context.Tenants
            .AnyAsync(
                t => t.Code == code &&
                     t.Id != id,
                cancellationToken);

        if (codeExists)
        {
            return Conflict(
                "A tenant with this code already exists.");
        }

        // ---------------------------------------------------------
        // VALIDATE DEFAULT CURRENCY
        // ---------------------------------------------------------

        var currency = await _context.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == dto.DefaultCurrencyId &&
                     c.IsActive,
                cancellationToken);

        if (currency == null)
        {
            return BadRequest(
                "Selected default currency is invalid or inactive.");
        }

        // ---------------------------------------------------------
        // UPDATE TENANT
        // ---------------------------------------------------------

        tenant.Name = dto.Name.Trim();
        tenant.Code = code;
        tenant.Domain = dto.Domain?.Trim() ?? string.Empty;
        tenant.Plan = dto.Plan?.Trim() ?? "Enterprise Pro";

        // ---------------------------------------------------------
        // WORKING SCHEDULE
        // ---------------------------------------------------------

        tenant.StandardWorkingHours =
            dto.StandardWorkingHours;

        tenant.WorkStartTime =
            dto.WorkStartTime;

        tenant.WorkEndTime =
            dto.WorkEndTime;

        // ---------------------------------------------------------
        // CURRENCY
        // ---------------------------------------------------------

        tenant.DefaultCurrencyId = currency.Id;

        // ---------------------------------------------------------
        // STATUS
        // ---------------------------------------------------------

        tenant.IsActive = dto.isActive;

        await _context.SaveChangesAsync(cancellationToken);

        // ---------------------------------------------------------
        // RETURN UPDATED TENANT
        // ---------------------------------------------------------

        return Ok(new
        {
            id = tenant.Id,
            name = tenant.Name,
            code = tenant.Code,
            domain = tenant.Domain,
            plan = tenant.Plan,

            // Currency
            defaultCurrencyId = tenant.DefaultCurrencyId,
            currency = currency.Code,
            currencyName = currency.Name,
            currencySymbol = currency.Symbol,

            // Working schedule
            standardWorkingHours = tenant.StandardWorkingHours,
            workStartTime = tenant.WorkStartTime,
            workEndTime = tenant.WorkEndTime,

            // Status / audit
            isActive = tenant.IsActive,
            createdAtUtc = tenant.CreatedAtUtc,
            updatedAtUtc = tenant.UpdatedAtUtc,
            createdByUserId = tenant.CreatedByUserId
        });
    }
}