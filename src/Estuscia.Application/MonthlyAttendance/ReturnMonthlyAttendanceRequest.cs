using System.ComponentModel.DataAnnotations;

namespace Estuscia.Application.MonthlyAttendance;

public sealed class ReturnMonthlyAttendanceRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}