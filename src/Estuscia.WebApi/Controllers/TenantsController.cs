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

                // Currency master information
                defaultCurrencyId = t.DefaultCurrencyId,
                currency = t.DefaultCurrency.Code,
                currencyName = t.DefaultCurrency.Name,
                currencySymbol = t.DefaultCurrency.Symbol,

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

        // ---------------------------------------------------------
        // VALIDATE DEFAULT CURRENCY
        // ---------------------------------------------------------

        var currency = await _context.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == dto.DefaultCurrencyId && c.IsActive,
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

            // New currency architecture
            DefaultCurrencyId = currency.Id,

            IsActive = true
        };

        _context.Tenants.Add(tenant);

        // First save is required to generate Tenant.Id
        await _context.SaveChangesAsync(cancellationToken);

        // ---------------------------------------------------------
        // CREATE INITIAL PAYMENT RECORD
        // ---------------------------------------------------------

        var tenantPayment = new TenantPayment
        {
            TenantId = tenant.Id,

            // Preserve the currency associated with this
            // tenant payment.
            CurrencyId = currency.Id,

            // No branches initially
            TotalBranches = 0,

            // Default payment mode
            PaymentMode = PaymentMode.Monthly,

            // Payment has not been made yet
            Amount = 0,
            PaymentStatus = PaymentStatus.Pending,

            PaymentDateUtc = null,
            ValidFromUtc = null,
            ValidUntilUtc = null,

            // Newly created tenant is not registered yet
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

            defaultCurrencyId = tenant.DefaultCurrencyId,
            currency = currency.Code,
            currencyName = currency.Name,
            currencySymbol = currency.Symbol,

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

        // ---------------------------------------------------------
        // VALIDATE DEFAULT CURRENCY
        // ---------------------------------------------------------

        var currency = await _context.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == dto.DefaultCurrencyId && c.IsActive,
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
        tenant.DefaultCurrencyId = currency.Id;
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

            defaultCurrencyId = tenant.DefaultCurrencyId,
            currency = currency.Code,
            currencyName = currency.Name,
            currencySymbol = currency.Symbol,

            isActive = tenant.IsActive,
            createdAtUtc = tenant.CreatedAtUtc,
            updatedAtUtc = tenant.UpdatedAtUtc,
            createdByUserId = tenant.CreatedByUserId
        });
    }
}