using Estuscia.Application.Common.DTOs.TenantPayment;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Estuscia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "super_admin")]
public class TenantPaymentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TenantPaymentsController(AppDbContext db)
    {
        _db = db;
    }

    // ============================================================
    // GET CURRENT PAYMENT STATUS
    // ============================================================

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentPayments(
        CancellationToken cancellationToken = default)
    {
        //var now = DateTime.UtcNow;

        var tenants = await _db.Tenants
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Code,
                x.Domain,
                x.Plan,
                x.Currency,
                x.IsActive
            })
            .ToListAsync(cancellationToken);

        var payments = await _db.TenantPayments
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Where(x => x.Tenant.IsActive)
            .OrderBy(x => x.TenantId)
            .ThenByDescending(x => x.ValidUntilUtc)
            .ToListAsync(cancellationToken);

        var result = new List<object>();

        foreach (var tenant in tenants)
        {
            var tenantPayments = payments
                .Where(x => x.TenantId == tenant.Id)
                .OrderByDescending(
                    x => x.ValidUntilUtc ?? DateTime.MinValue)
                .ToList();

            if (tenantPayments.Count == 0)
            {
                result.Add(new
                {
                    tenantId = tenant.Id,
                    tenantName = tenant.Name,
                    tenantCode = tenant.Code,
                    tenantCurrency = tenant.Currency,

                    paymentId = (int?)null,

                    totalBranches = 0,

                    paymentMode = (PaymentMode?)null,

                    amount = 0m,

                    paymentStatus = PaymentStatus.Pending,

                    paymentDateUtc = (DateTime?)null,

                    validFromUtc = (DateTime?)null,

                    validUntilUtc = (DateTime?)null,

                    registrationStatus = false,

                    notes = (string?)null
                });

                continue;
            }

            /*
             * The payment with the furthest coverage is the
             * authoritative current/future payment.
             *
             * This handles advance payments correctly.
             */
            var currentPayment = tenantPayments.First();

            /*
             * If the latest period has expired and the background
             * worker has not yet generated the next record, we
             * expose it as Pending.
             *
             * Normally the worker will already have created it.
             */
            var status = currentPayment.PaymentStatus;

            //if (currentPayment.ValidUntilUtc.HasValue &&
            //    currentPayment.ValidUntilUtc.Value < now &&
            //    currentPayment.PaymentStatus == PaymentStatus.Paid)
            //{
            //    status = PaymentStatus.Pending;
            //}

            result.Add(new
            {
                tenantId = tenant.Id,
                tenantName = tenant.Name,
                tenantCode = tenant.Code,
                tenantCurrency = tenant.Currency,

                paymentId = currentPayment.Id,

                totalBranches = currentPayment.TotalBranches,

                paymentMode = currentPayment.PaymentMode,

                amount = currentPayment.Amount,

                paymentStatus = status,

                paymentDateUtc =
                    currentPayment.PaymentDateUtc,

                validFromUtc =
                    currentPayment.ValidFromUtc,

                validUntilUtc =
                    currentPayment.ValidUntilUtc,

                registrationStatus =
                    currentPayment.RegistrationStatus,

                notes = currentPayment.Notes
            });
        }

        return Ok(result);
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
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.TenantPayments
            .AsNoTracking()
            .Include(x => x.Tenant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals(
                "Paid",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(
                    x => x.PaymentStatus == PaymentStatus.Paid);
            }
            else if (
                status.Equals(
                    "Pending",
                    StringComparison.OrdinalIgnoreCase) ||
                status.Equals(
                    "Unpaid",
                    StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(
                    x => x.PaymentStatus == PaymentStatus.Pending);
            }
        }

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
                (x.PaymentDateUtc.HasValue &&
                 x.PaymentDateUtc.Value >= fromDate) ||
                (x.ValidFromUtc.HasValue &&
                 x.ValidFromUtc.Value >= fromDate));
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
                (x.PaymentDateUtc.HasValue &&
                 x.PaymentDateUtc.Value < toDate) ||
                (x.ValidFromUtc.HasValue &&
                 x.ValidFromUtc.Value < toDate));
        }

        var totalCount =
            await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(
                x => x.ValidUntilUtc)
            .ThenByDescending(
                x => x.Id)
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
            items,
            totalCount,
            page,
            pageSize,
            totalPages =
                (int)Math.Ceiling(
                    totalCount / (double)pageSize)
        });
    }

    // ============================================================
    // GET PAYMENT BY ID
    // ============================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPayment(
        int id,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.TenantPayments
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
        {
            return NotFound(
                new
                {
                    message = "Payment record not found."
                });
        }

        return Ok(payment);
    }

    // ============================================================
    // GET TENANT PAYMENT HISTORY
    // ============================================================

    [HttpGet("tenant/{tenantId:int}")]
    public async Task<IActionResult> GetTenantPayments(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantExists = await _db.Tenants
            .AnyAsync(
                x => x.Id == tenantId,
                cancellationToken);

        if (!tenantExists)
        {
            return NotFound(
                new
                {
                    message = "Tenant not found."
                });
        }

        var payments = await _db.TenantPayments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(
                x => x.ValidFromUtc)
            .ThenByDescending(
                x => x.Id)
            .Select(x => new
            {
                id = x.Id,
                tenantId = x.TenantId,

                totalBranches = x.TotalBranches,

                paymentMode = x.PaymentMode,

                amount = x.Amount,

                paymentStatus = x.PaymentStatus,

                paymentDateUtc =
                    x.PaymentDateUtc,

                validFromUtc =
                    x.ValidFromUtc,

                validUntilUtc =
                    x.ValidUntilUtc,

                registrationStatus =
                    x.RegistrationStatus,

                notes = x.Notes,

                createdAtUtc =
                    x.CreatedAtUtc,

                updatedAtUtc =
                    x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(payments);
    }

    // ============================================================
    // CREATE PAYMENT
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> CreatePayment(
        CreateTenantPaymentDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.TenantId <= 0)
        {
            return BadRequest(
                "Tenant is required.");
        }

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(
                x => x.Id == dto.TenantId,
                cancellationToken);

        if (tenant == null)
        {
            return BadRequest(
                "Tenant not found.");
        }

        if (dto.TotalBranches < 0)
        {
            return BadRequest(
                "Total branches cannot be negative.");
        }

        if (dto.Amount < 0)
        {
            return BadRequest(
                "Amount cannot be negative.");
        }

        if (dto.ValidFromUtc.HasValue &&
            dto.ValidUntilUtc.HasValue &&
            dto.ValidUntilUtc.Value < dto.ValidFromUtc.Value)
        {
            return BadRequest(
                "Valid until date cannot be before valid from date.");
        }

        if (dto.PaymentStatus == PaymentStatus.Paid &&
            !dto.PaymentDateUtc.HasValue)
        {
            return BadRequest(
                "Payment date is required when payment status is Paid.");
        }

        /*
         * Prevent duplicate billing period.
         */
        if (dto.ValidFromUtc.HasValue &&
            dto.ValidUntilUtc.HasValue)
        {
            var duplicate = await _db.TenantPayments
                .AnyAsync(
                    x =>
                        x.TenantId == dto.TenantId &&
                        x.ValidFromUtc == dto.ValidFromUtc &&
                        x.ValidUntilUtc == dto.ValidUntilUtc,
                    cancellationToken);

            if (duplicate)
            {
                return Conflict(
                    new
                    {
                        message =
                            "A payment already exists for this billing period."
                    });
            }
        }

        var payment = new TenantPayment
        {
            TenantId = dto.TenantId,
            TotalBranches = dto.TotalBranches,
            PaymentMode = dto.PaymentMode,
            Amount = dto.Amount,
            PaymentStatus = dto.PaymentStatus,
            PaymentDateUtc = dto.PaymentDateUtc,
            ValidFromUtc = dto.ValidFromUtc,
            ValidUntilUtc = dto.ValidUntilUtc,
            RegistrationStatus = dto.RegistrationStatus,
            Notes = dto.Notes
        };

        _db.TenantPayments.Add(payment);

        await _db.SaveChangesAsync(
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
                totalBranches = payment.TotalBranches,
                paymentMode = payment.PaymentMode,
                amount = payment.Amount,
                paymentStatus = payment.PaymentStatus,
                paymentDateUtc = payment.PaymentDateUtc,
                validFromUtc = payment.ValidFromUtc,
                validUntilUtc = payment.ValidUntilUtc,
                registrationStatus =
                    payment.RegistrationStatus,
                notes = payment.Notes,
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
        UpdateTenantPaymentDto dto,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.TenantPayments
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (payment == null)
        {
            return NotFound(
                new
                {
                    message = "Payment record not found."
                });
        }

        if (dto.TotalBranches < 0)
        {
            return BadRequest(
                "Total branches cannot be negative.");
        }

        if (dto.Amount < 0)
        {
            return BadRequest(
                "Amount cannot be negative.");
        }

        if (dto.ValidFromUtc.HasValue &&
            dto.ValidUntilUtc.HasValue &&
            dto.ValidUntilUtc.Value < dto.ValidFromUtc.Value)
        {
            return BadRequest(
                "Valid until date cannot be before valid from date.");
        }

        if (dto.PaymentStatus == PaymentStatus.Paid &&
            !dto.PaymentDateUtc.HasValue)
        {
            return BadRequest(
                "Payment date is required when payment status is Paid.");
        }

        if (dto.ValidFromUtc.HasValue &&
            dto.ValidUntilUtc.HasValue)
        {
            var duplicate = await _db.TenantPayments
                .AnyAsync(
                    x =>
                        x.Id != id &&
                        x.TenantId == payment.TenantId &&
                        x.ValidFromUtc == dto.ValidFromUtc &&
                        x.ValidUntilUtc == dto.ValidUntilUtc,
                    cancellationToken);

            if (duplicate)
            {
                return Conflict(
                    new
                    {
                        message =
                            "Another payment already exists for this billing period."
                    });
            }
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
            dto.Notes;

        await _db.SaveChangesAsync(
            cancellationToken);

        return Ok(payment);
    }

    // ============================================================
    // DELETE PAYMENT
    // ============================================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePayment(
        int id,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.TenantPayments
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (payment == null)
        {
            return NotFound(
                new
                {
                    message = "Payment record not found."
                });
        }

        var tenantPaymentCount =
            await _db.TenantPayments
                .CountAsync(
                    x => x.TenantId == payment.TenantId,
                    cancellationToken);

        if (tenantPaymentCount <= 1)
        {
            return BadRequest(
                "The last payment record for a tenant cannot be deleted.");
        }

        _db.TenantPayments.Remove(payment);

        await _db.SaveChangesAsync(
            cancellationToken);

        return NoContent();
    }
}