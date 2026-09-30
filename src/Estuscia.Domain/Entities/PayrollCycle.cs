using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class PayrollCycle : BaseEntity, IMultiTenantEntity
{
    public int? TenantId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public string MonthYear { get; set; } = string.Empty;

    public PayrollCycleStatus Status { get; set; }

    public decimal TotalBasicSalary { get; set; }

    public decimal TotalBonus { get; set; }

    public decimal TotalDeduction { get; set; }

    public decimal TotalNetSalary { get; set; }

    // HR submission
    public int? SubmittedByUserId { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    // Company Admin approval
    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    // Rejection
    public int? RejectedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    // Payment
    public int? PaidByUserId { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public bool IsLocked { get; set; }

    public ICollection<PayrollRecord> PayrollRecords { get; set; }
        = new List<PayrollRecord>();
}