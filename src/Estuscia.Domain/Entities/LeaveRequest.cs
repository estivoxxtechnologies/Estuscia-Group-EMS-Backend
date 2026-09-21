using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class LeaveRequest
{
    public int Id { get; set; }

    // ============================================================
    // TENANT / BRANCH
    // ============================================================

    public int TenantId { get; set; }

    public int BranchId { get; set; }

    // ============================================================
    // EMPLOYEE
    // ============================================================

    public int UserId { get; set; }

    // ============================================================
    // LEAVE DETAILS
    // ============================================================

    public LeaveType LeaveType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal RequestedDays { get; set; }

    public string Reason { get; set; } = string.Empty;

    // ============================================================
    // STATUS
    // ============================================================

    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;

    // ============================================================
    // MEDICAL CERTIFICATE
    // Required for Sick Leave
    // ============================================================

    public string? MedicalCertificateFileUrl { get; set; }

    public string? MedicalCertificateFileName { get; set; }

    // ============================================================
    // HR REVIEW
    // ============================================================

    public int? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewReason { get; set; }

    // ============================================================
    // AUDIT
    // ============================================================

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int? CreatedByUserId { get; set; }

    // ============================================================
    // NAVIGATION PROPERTIES
    // ============================================================

    public Tenant? Tenant { get; set; }

    public TenantBranch? Branch { get; set; }

    public ApplicationUser? User { get; set; }

    public ApplicationUser? ReviewedByUser { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}