using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class SalesLeadAssignment : BaseEntity, IBranchScopedEntity
{
    public int? TenantId { get; set; }

    public int BranchId { get; set; }

    public int SalesLeadId { get; set; }

    public int AssignedToUserId { get; set; }

    public int AssignedByUserId { get; set; }

    public DateTime AssignedAtUtc { get; set; }

    public SalesLeadOutcome Outcome { get; set; }
        = SalesLeadOutcome.Pending;

    public string? Notes { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    // Navigation properties

    public Tenant? Tenant { get; set; }

    public TenantBranch? Branch { get; set; }

    public SalesLead SalesLead { get; set; } = null!;

    public ApplicationUser AssignedToUser { get; set; } = null!;

    public ApplicationUser AssignedByUser { get; set; } = null!;
}