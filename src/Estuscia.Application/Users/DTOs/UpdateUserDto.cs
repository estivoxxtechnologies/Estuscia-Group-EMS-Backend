namespace Estuscia.Application.Users.DTOs;

public class UpdateUserDto
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string EmployeeCode { get; set; } = string.Empty;

    public int RoleNumber { get; set; }

    public string Designation { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public decimal SalaryBase { get; set; }

    public int BranchId { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; }
}