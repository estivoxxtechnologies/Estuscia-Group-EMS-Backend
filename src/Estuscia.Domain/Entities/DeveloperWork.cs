using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class DeveloperWork : BaseEntity, IBranchScopedEntity
{
    public int? TenantId { get; set; }

    public int BranchId { get; set; }

    // ============================================================
    // WORK DETAILS
    // ============================================================

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DeveloperWorkType WorkType { get; set; } =
        DeveloperWorkType.Other;

    public DeveloperWorkPriority Priority { get; set; } =
        DeveloperWorkPriority.Medium;

    public DeveloperWorkStatus Status { get; set; } =
        DeveloperWorkStatus.Pending;

    // ============================================================
    // ASSIGNMENT
    // ============================================================

    public int AssignedToUserId { get; set; }

    public int AssignedByUserId { get; set; }

    public DateTime AssignedAtUtc { get; set; }

    // ============================================================
    // DATES
    // ============================================================

    public DateTime? DueDateUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    // ============================================================
    // COMPLETION
    // ============================================================

    public string? CompletionNotes { get; set; }

    // ============================================================
    // NAVIGATION
    // ============================================================

    public Tenant? Tenant { get; set; }

    public TenantBranch? Branch { get; set; }

    public ApplicationUser AssignedToUser { get; set; } = null!;

    public ApplicationUser AssignedByUser { get; set; } = null!;
}