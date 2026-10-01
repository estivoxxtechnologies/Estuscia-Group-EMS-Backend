using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class PayrollRecord : BaseEntity, IMultiTenantEntity
{
    public int? TenantId { get; set; }

    public int PayrollCycleId { get; set; }

    public int UserId { get; set; }

    // Snapshot from Users.SalaryBase
    public decimal BasicSalary { get; set; }

    // Approved adjustments
    public decimal TotalBonus { get; set; }

    public decimal TotalDeduction { get; set; }

    // Basic + Bonus - Deduction
    public decimal NetSalary { get; set; }

    public PayrollCycleStatus Status { get; set; }

    // HR submission
    public int? SubmittedByUserId { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    // Company Admin approval
    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    // Payment
    public int? PaidByUserId { get; set; }

    public PayrollPaymentStatus PaymentStatus { get; set; }
        = PayrollPaymentStatus.Unpaid;

    public DateTime? PaidAtUtc { get; set; }

    public bool IsLocked { get; set; }

    public ApplicationUser? User { get; set; }

    public PayrollCycle? PayrollCycle { get; set; }
}