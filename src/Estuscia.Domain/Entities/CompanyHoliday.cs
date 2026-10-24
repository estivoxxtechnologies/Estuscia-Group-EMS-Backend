using Estuscia.Domain.Common;

namespace Estuscia.Domain.Entities;

public class CompanyHoliday : BaseEntity, IMultiTenantEntity
{
    public int? TenantId { get; set; }
    public int? BranchId { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? UpdatedByUserId { get; set; }

    public TenantBranch? Branch { get; set; }
}