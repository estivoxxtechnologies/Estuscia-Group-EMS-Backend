using Estuscia.Domain.Entities;
using Estuscia.Domain.Enums;
using Estuscia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.Infrastructure.Services;

public class TenantPaymentBillingService
{
    private readonly AppDbContext _db;

    public TenantPaymentBillingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task GeneratePendingPaymentsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var tenants = await _db.Tenants
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            await EnsureNextPaymentAsync(
                tenant,
                now,
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNextPaymentAsync(
        Tenant tenant,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var payments = await _db.TenantPayments
            .Where(x => x.TenantId == tenant.Id)
            .OrderByDescending(x => x.ValidUntilUtc)
            .ToListAsync(cancellationToken);

        /*
         * No payment history.
         *
         * We don't automatically invent the first billing
         * period here because the tenant should normally receive
         * its first payment when the tenant/payment is created.
         */
        if (payments.Count == 0)
            return;

        /*
         * The payment with the furthest ValidUntil date is the
         * latest known coverage.
         *
         * This is important for advance payments.
         */
        var latestPayment = payments
            .OrderByDescending(x => x.ValidUntilUtc ?? DateTime.MinValue)
            .First();

        if (!latestPayment.ValidFromUtc.HasValue ||
            !latestPayment.ValidUntilUtc.HasValue)
        {
            return;
        }

        /*
         * If the latest known payment period still covers today,
         * nothing needs to be generated.
         */
        if (latestPayment.ValidUntilUtc.Value >= now)
        {
            return;
        }

        /*
         * The tenant's latest coverage has expired.
         *
         * Calculate the next period.
         */
        var nextPeriod = CalculateNextPeriod(
            latestPayment,
            latestPayment.ValidUntilUtc.Value);

        /*
         * Prevent duplicates even if this service executes
         * multiple times.
         */
        var alreadyExists = await _db.TenantPayments
            .AnyAsync(
                x =>
                    x.TenantId == tenant.Id &&
                    x.ValidFromUtc == nextPeriod.ValidFrom &&
                    x.ValidUntilUtc == nextPeriod.ValidUntil,
                cancellationToken);

        if (alreadyExists)
        {
            return;
        }

        var pendingPayment = new TenantPayment
        {
            TenantId = tenant.Id,

            TotalBranches = latestPayment.TotalBranches,

            PaymentMode = latestPayment.PaymentMode,

            Amount = latestPayment.Amount,

            PaymentStatus = PaymentStatus.Pending,

            PaymentDateUtc = null,

            ValidFromUtc = nextPeriod.ValidFrom,

            ValidUntilUtc = nextPeriod.ValidUntil,

            RegistrationStatus = latestPayment.RegistrationStatus,

            Notes = $"Automatically generated billing period for " +
                    $"{nextPeriod.ValidFrom:dd-MMM-yyyy} to " +
                    $"{nextPeriod.ValidUntil:dd-MMM-yyyy}"
        };

        _db.TenantPayments.Add(pendingPayment);
    }

    private static (
        DateTime ValidFrom,
        DateTime ValidUntil
    ) CalculateNextPeriod(
        TenantPayment previousPayment,
        DateTime previousValidUntil)
    {
        var nextValidFrom = previousValidUntil.Date.AddDays(1);

        DateTime nextValidUntil;

        switch (previousPayment.PaymentMode)
        {
            case PaymentMode.Monthly:
                nextValidUntil = nextValidFrom
                    .AddMonths(1)
                    .AddTicks(-1);
                break;

            case PaymentMode.Quarterly:
                nextValidUntil = nextValidFrom
                    .AddMonths(3)
                    .AddTicks(-1);
                break;

            case PaymentMode.HalfYearly:
                nextValidUntil = nextValidFrom
                    .AddMonths(6)
                    .AddTicks(-1);
                break;

            case PaymentMode.Yearly:
                nextValidUntil = nextValidFrom
                    .AddYears(1)
                    .AddTicks(-1);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported payment mode: " +
                    $"{previousPayment.PaymentMode}");
        }

        return (
            nextValidFrom,
            nextValidUntil
        );
    }
}