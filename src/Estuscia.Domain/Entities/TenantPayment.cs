using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class TenantPayment : BaseEntity
{
    public int TenantId { get; set; }

    public int TotalBranches { get; set; }

    public PaymentMode PaymentMode { get; set; }

    // Currency actually used for this payment
    public int CurrencyId { get; set; }
    public Currency Currency { get; set; } = null!;

    public decimal Amount { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public DateTime? PaymentDateUtc { get; set; }

    public DateTime? ValidFromUtc { get; set; }

    public DateTime? ValidUntilUtc { get; set; }

    public bool RegistrationStatus { get; set; }

    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = null!;
}