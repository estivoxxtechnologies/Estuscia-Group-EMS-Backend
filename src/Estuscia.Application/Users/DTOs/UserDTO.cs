namespace Estuscia.Application.Users.DTOs;

public class UserDto
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;

    public int? BranchId { get; set; }
    public string? BranchName { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string EmployeeCode { get; set; } = string.Empty;

    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    public decimal SalaryBase { get; set; }

    public string AvatarUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}