using Estuscia.Application.MonthlyAttendance;

namespace Estuscia.Application.Common.Interfaces;

public interface IMonthlyAttendanceService
{
    Task<object> GenerateAsync(
        int branchId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}