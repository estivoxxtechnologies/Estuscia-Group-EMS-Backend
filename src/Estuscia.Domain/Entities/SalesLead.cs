using Estuscia.Domain.Common;

namespace Estuscia.Domain.Entities;

public class SalesLead : BaseEntity, IBranchScopedEntity
{
    public int? TenantId { get; set; }

    public int BranchId { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties

    public Tenant? Tenant { get; set; }

    public TenantBranch? Branch { get; set; }

    public ICollection<SalesLeadAssignment> Assignments { get; set; }
        = new List<SalesLeadAssignment>();
}