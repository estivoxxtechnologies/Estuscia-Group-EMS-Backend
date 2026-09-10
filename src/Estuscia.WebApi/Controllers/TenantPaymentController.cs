using Estuscia.Application.Common.DTOs.TenantPayment;
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
public class TenantPaymentsController : ControllerBase
{
    private readonly IAppDbContext _context;

    public TenantPaymentsController(IAppDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // GET ALL PAYMENT HISTORY
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetPayments(
        [FromQuery] string? status,
        [FromQuery] int? fromYear,
        [FromQuery] int? toYear,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            page = 1;

        if (pageSize < 1)
            pageSize = 25;

        if (pageSize > 100)
            pageSize = 100;

        var query = _context.TenantPayments
            .AsNoTracking()
            .Include(x => x.Tenant)
            .AsQueryable();

        // --------------------------------------------------------
        // STATUS FILTER
        // --------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.PaymentStatus == PaymentStatus.Paid);
            }
            else if (status.Equals("Unpaid", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.PaymentStatus == PaymentStatus.Pending);
            }
        }

        // --------------------------------------------------------
        // YEAR FILTER
        // --------------------------------------------------------

        if (fromYear.HasValue)
        {
            var fromDate = new DateTime(
                fromYear.Value,
                1,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

            query = query.Where(x =>
                x.PaymentDateUtc >= fromDate ||
                x.ValidFromUtc >= fromDate);
        }

        if (toYear.HasValue)
        {
            var toDate = new DateTime(
                toYear.Value + 1,
                1,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

            query = query.Where(x =>
                x.PaymentDateUtc < toDate ||
                x.ValidFromUtc < toDate);
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var payments = await query
            .OrderByDescending(x => x.PaymentDateUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id = x.Id,

                tenantId = x.TenantId,

                tenant = new
                {
                    id = x.Tenant.Id,
                    name = x.Tenant.Name,
                    code = x.Tenant.Code,
                    domain = x.Tenant.Domain,
                    plan = x.Tenant.Plan,
                    currency = x.Tenant.Currency,
                    isActive = x.Tenant.IsActive
                },

                totalBranches = x.TotalBranches,

                paymentMode = x.PaymentMode,

                amount = x.Amount,

                paymentStatus = x.PaymentStatus,

                paymentDateUtc = x.PaymentDateUtc,

                validFromUtc = x.ValidFromUtc,

                validUntilUtc = x.ValidUntilUtc,

                registrationStatus =
                    x.RegistrationStatus,

                notes = x.Notes,

                createdAtUtc = x.CreatedAtUtc,

                updatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            items = payments,

            totalCount,

            page,

            pageSize,

            totalPages =
                (int)Math.Ceiling(
                    totalCount /
                    (double)pageSize)
        });
    }

    // ============================================================
    // GET SINGLE PAYMENT
    // ============================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPayment(
        int id,
        CancellationToken cancellationToken)
    {
        var payment = await _context.TenantPayments
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Where(x => x.Id == id)
            .Select(x => new
            {
                id = x.Id,

                tenantId = x.TenantId,

                tenant = new
                {
                    id = x.Tenant.Id,
                    name = x.Tenant.Name,
                    code = x.Tenant.Code,
                    domain = x.Tenant.Domain,
                    plan = x.Tenant.Plan,
                    currency = x.Tenant.Currency,
                    isActive = x.Tenant.IsActive
                },

                totalBranches = x.TotalBranches,

                paymentMode = x.PaymentMode,

                amount = x.Amount,

                paymentStatus = x.PaymentStatus,

                paymentDateUtc = x.PaymentDateUtc,

                validFromUtc = x.ValidFromUtc,

                validUntilUtc = x.ValidUntilUtc,

                registrationStatus =
                    x.RegistrationStatus,

                notes = x.Notes,

                createdAtUtc = x.CreatedAtUtc,

                updatedAtUtc = x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(
                cancellationToken);

        if (payment == null)
            return NotFound("Payment record not found.");

        return Ok(payment);
    }

    // ============================================================
    // GET TENANT PAYMENT HISTORY
    // ============================================================

    [HttpGet("tenant/{tenantId:int}")]
    public async Task<IActionResult> GetTenantPayments(
        int tenantId,
        CancellationToken cancellationToken)
    {
        var tenantExists = await _context.Tenants
            .AnyAsync(
                x => x.Id == tenantId,
                cancellationToken);

        if (!tenantExists)
            return NotFound("Tenant not found.");

        var payments = await _context.TenantPayments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.PaymentDateUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                id = x.Id,

                tenantId = x.TenantId,

                totalBranches = x.TotalBranches,

                paymentMode = x.PaymentMode,

                amount = x.Amount,

                paymentStatus = x.PaymentStatus,

                paymentDateUtc = x.PaymentDateUtc,

                validFromUtc = x.ValidFromUtc,

                validUntilUtc = x.ValidUntilUtc,

                registrationStatus =
                    x.RegistrationStatus,

                notes = x.Notes,

                createdAtUtc = x.CreatedAtUtc,

                updatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(payments);
    }

    // ============================================================
    // CREATE PAYMENT
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> CreatePayment(
        [FromBody] CreateTenantPaymentDto dto,
        CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(
                x => x.Id == dto.TenantId,
                cancellationToken);

        if (tenant == null)
            return NotFound("Tenant not found.");

        if (dto.TotalBranches < 0)
            return BadRequest(
                "Total branches cannot be negative.");

        if (dto.Amount < 0)
            return BadRequest(
                "Payment amount cannot be negative.");

        if (dto.ValidFromUtc.HasValue &&
            dto.ValidUntilUtc.HasValue &&
            dto.ValidUntilUtc.Value < dto.ValidFromUtc.Value)
        {
            return BadRequest(
                "Valid until date cannot be earlier than valid from date.");
        }

        if (dto.PaymentStatus == PaymentStatus.Paid &&
            !dto.PaymentDateUtc.HasValue)
        {
            return BadRequest(
                "Payment date is required when payment status is Paid.");
        }

        var payment = new TenantPayment
        {
            TenantId = tenant.Id,

            TotalBranches =
                dto.TotalBranches,

            PaymentMode =
                dto.PaymentMode,

            Amount =
                dto.Amount,

            PaymentStatus =
                dto.PaymentStatus,

            PaymentDateUtc =
                dto.PaymentDateUtc,

            ValidFromUtc =
                dto.ValidFromUtc,

            ValidUntilUtc =
                dto.ValidUntilUtc,

            RegistrationStatus =
                dto.RegistrationStatus,

            Notes =
                string.IsNullOrWhiteSpace(dto.Notes)
                    ? null
                    : dto.Notes.Trim()
        };

        _context.TenantPayments.Add(payment);

        await _context.SaveChangesAsync(
            cancellationToken);

        return CreatedAtAction(
            nameof(GetPayment),
            new
            {
                id = payment.Id
            },
            new
            {
                id = payment.Id,

                tenantId = payment.TenantId,

                totalBranches =
                    payment.TotalBranches,

                paymentMode =
                    payment.PaymentMode,

                amount =
                    payment.Amount,

                paymentStatus =
                    payment.PaymentStatus,

                paymentDateUtc =
                    payment.PaymentDateUtc,

                validFromUtc =
                    payment.ValidFromUtc,

                validUntilUtc =
                    payment.ValidUntilUtc,

                registrationStatus =
                    payment.RegistrationStatus,

                notes =
                    payment.Notes,

                createdAtUtc =
                    payment.CreatedAtUtc,

                updatedAtUtc =
                    payment.UpdatedAtUtc
            });
    }

    // ============================================================
    // UPDATE PAYMENT
    // ============================================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePayment(
        int id,
        [FromBody] UpdateTenantPaymentDto dto,
        CancellationToken cancellationToken)
    {
        var payment = await _context.TenantPayments
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (payment == null)
            return NotFound("Payment record not found.");

        if (dto.TotalBranches < 0)
            return BadRequest(
                "Total branches cannot be negative.");

        if (dto.Amount < 0)
            return BadRequest(
                "Payment amount cannot be negative.");

        if (dto.ValidFromUtc.HasValue &&
            dto.ValidUntilUtc.HasValue &&
            dto.ValidUntilUtc.Value < dto.ValidFromUtc.Value)
        {
            return BadRequest(
                "Valid until date cannot be earlier than valid from date.");
        }

        if (dto.PaymentStatus == PaymentStatus.Paid &&
            !dto.PaymentDateUtc.HasValue)
        {
            return BadRequest(
                "Payment date is required when payment status is Paid.");
        }

        payment.TotalBranches =
            dto.TotalBranches;

        payment.PaymentMode =
            dto.PaymentMode;

        payment.Amount =
            dto.Amount;

        payment.PaymentStatus =
            dto.PaymentStatus;

        payment.PaymentDateUtc =
            dto.PaymentDateUtc;

        payment.ValidFromUtc =
            dto.ValidFromUtc;

        payment.ValidUntilUtc =
            dto.ValidUntilUtc;

        payment.RegistrationStatus =
            dto.RegistrationStatus;

        payment.Notes =
            string.IsNullOrWhiteSpace(dto.Notes)
                ? null
                : dto.Notes.Trim();

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(new
        {
            id = payment.Id,

            tenantId = payment.TenantId,

            totalBranches =
                payment.TotalBranches,

            paymentMode =
                payment.PaymentMode,

            amount =
                payment.Amount,

            paymentStatus =
                payment.PaymentStatus,

            paymentDateUtc =
                payment.PaymentDateUtc,

            validFromUtc =
                payment.ValidFromUtc,

            validUntilUtc =
                payment.ValidUntilUtc,

            registrationStatus =
                payment.RegistrationStatus,

            notes =
                payment.Notes,

            createdAtUtc =
                payment.CreatedAtUtc,

            updatedAtUtc =
                payment.UpdatedAtUtc
        });
    }

    // ============================================================
    // DELETE PAYMENT
    // ============================================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePayment(
        int id,
        CancellationToken cancellationToken)
    {
        var payment = await _context.TenantPayments
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (payment == null)
            return NotFound("Payment record not found.");

        // --------------------------------------------------------
        // NEVER ALLOW THE LAST PAYMENT TO BE DELETED
        // --------------------------------------------------------

        var paymentCount =
            await _context.TenantPayments
                .CountAsync(
                    x => x.TenantId == payment.TenantId,
                    cancellationToken);

        if (paymentCount <= 1)
        {
            return BadRequest(
                "Cannot delete the last payment record for this tenant. " +
                "At least one payment record is required.");
        }

        _context.TenantPayments.Remove(payment);

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(new
        {
            message =
                "Payment record deleted successfully."
        });
    }
}