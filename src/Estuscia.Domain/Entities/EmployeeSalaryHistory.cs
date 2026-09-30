using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class EmployeeSalaryHistory : BaseEntity, IMultiTenantEntity
{
    public int? TenantId { get; set; }

    public int UserId { get; set; }

    public int? BranchId { get; set; }

    // Salary currently stored in Users.SalaryBase
    // before this change was requested.
    public decimal PreviousSalary { get; set; }

    // Salary HR wants to change it to.
    public decimal NewSalary { get; set; }

    // HR's reason for changing the salary.
    public string? Reason { get; set; }

    // PendingApproval / Approved / Rejected
    public SalaryChangeStatus Status { get; set; }

    // Company Admin approval
    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    // Company Admin rejection
    public int? RejectedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    // Employee
    public ApplicationUser? User { get; set; }
}