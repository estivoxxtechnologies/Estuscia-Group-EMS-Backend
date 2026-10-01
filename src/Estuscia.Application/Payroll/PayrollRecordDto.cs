public class PayrollRecordDto
{
    public int Id { get; set; }

    public int PayrollCycleId { get; set; }

    public int UserId { get; set; }

    public string? EmployeeCode { get; set; }

    public string? EmployeeName { get; set; }

    public string? Designation { get; set; }

    public string? Department { get; set; }

    public int? BranchId { get; set; }

    public decimal BasicSalary { get; set; }

    public decimal TotalBonus { get; set; }

    public decimal TotalDeduction { get; set; }

    public decimal NetSalary { get; set; }

    public string Status { get; set; }
        = string.Empty;

    public string PaymentStatus { get; set; }
        = string.Empty;

    public bool IsLocked { get; set; }

    public int? SubmittedByUserId { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public int? PaidByUserId { get; set; }

    public DateTime? PaidAtUtc { get; set; }
}