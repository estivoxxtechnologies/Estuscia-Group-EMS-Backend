
using Estuscia.Domain.Common;

namespace Estuscia.Domain.Entities;

public class BranchWorkingDay : BaseEntity, IMultiTenantEntity
{
    public int? TenantId { get; set; }

    public int BranchId { get; set; }

    // DayOfWeek: Sunday = 0, Monday = 1, ... Saturday = 6
    public DayOfWeek DayOfWeek { get; set; }

    public bool IsWorkingDay { get; set; }

    public TenantBranch? Branch { get; set; }
}
