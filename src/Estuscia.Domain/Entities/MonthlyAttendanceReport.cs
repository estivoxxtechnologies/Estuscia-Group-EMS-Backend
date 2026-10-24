
using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;

namespace Estuscia.Domain.Entities;

public class MonthlyAttendanceReport : BaseEntity, IMultiTenantEntity
{
    public int? TenantId { get; set; }
    public int BranchId { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    public MonthlyAttendanceReportStatus Status { get; set; }
        = MonthlyAttendanceReportStatus.Draft;

    public int? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }

    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public int? ReturnedByUserId { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }

    public string? ReturnReason { get; set; }

    public TenantBranch? Branch { get; set; }
    public ICollection<MonthlyAttendanceSummary> Employees { get; set; }
        = new List<MonthlyAttendanceSummary>();
}
