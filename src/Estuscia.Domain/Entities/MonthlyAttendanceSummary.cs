
using Estuscia.Domain.Common;

namespace Estuscia.Domain.Entities;

public class MonthlyAttendanceSummary : BaseEntity, IMultiTenantEntity
{
    public int? TenantId { get; set; }
    public int BranchId { get; set; }
    public int ReportId { get; set; }
    public int UserId { get; set; }

    public int WorkingDays { get; set; }
    public decimal PresentDays { get; set; }
    public decimal ApprovedLeaveDays { get; set; }
    public decimal HalfDays { get; set; }
    public decimal AbsentDays { get; set; }

    public int LateArrivals { get; set; }
    public int MissingCheckIns { get; set; }
    public int MissingCheckOuts { get; set; }

    public decimal TotalWorkedHours { get; set; }
    public decimal RequiredWorkingHours { get; set; }
    public int DaysBelowRequiredHours { get; set; }
    public decimal TotalShortageHours { get; set; }

    public DateTime CalculatedAtUtc { get; set; }

    public MonthlyAttendanceReport? Report { get; set; }
    public ApplicationUser? User { get; set; }
    public TenantBranch? Branch { get; set; }
}
