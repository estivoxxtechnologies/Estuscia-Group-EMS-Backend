using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class PayrollAdjustment : BaseEntity, IMultiTenantEntity
{
    public int? TenantId { get; set; }

    public int PayrollCycleId { get; set; }

    public int UserId { get; set; }

    public PayrollAdjustmentType Type { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public PayrollAdjustmentStatus Status { get; set; }

    // Approval
    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    // Rejection
    public int? RejectedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    public ApplicationUser? User { get; set; }

    public PayrollCycle? PayrollCycle { get; set; }
}