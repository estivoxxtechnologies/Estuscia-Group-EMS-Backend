using System.ComponentModel.DataAnnotations;

namespace Estuscia.Application.MonthlyAttendance;

public sealed class GenerateMonthlyAttendanceRequest
{
    [Range(1, int.MaxValue)]
    public int BranchId { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }
}