namespace Estuscia.Application.Payroll;

public record CreateSalaryChangeRequest(
    int UserId,
    decimal NewSalary,
    string? Reason,
    int? BranchId
);

public record RejectSalaryChangeRequest(
    string? RejectionReason
);

public record SalaryHistoryDto(
    int Id,
    int UserId,
    string EmployeeName,
    string EmployeeCode,
    decimal PreviousSalary,
    decimal NewSalary,
    string? Reason,
    int Status,
    string StatusName,
    int? ApprovedByUserId,
    DateTime? ApprovedAtUtc,
    int? RejectedByUserId,
    DateTime? RejectedAtUtc,
    string? RejectionReason,
    DateTime CreatedAtUtc
);

public record CurrentSalaryDto(
    int UserId,
    string EmployeeName,
    string EmployeeCode,
    decimal CurrentSalary,
    int? BranchId
);