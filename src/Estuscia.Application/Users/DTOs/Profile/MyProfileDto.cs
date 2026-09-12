namespace Estuscia.Application.Users.DTOs.Profile;

public class MyProfileDto
{
    public int Id { get; set; }

    // ------------------------------------------------------------
    // PERSONAL INFORMATION
    // ------------------------------------------------------------

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string AvatarUrl { get; set; } = string.Empty;

    // ------------------------------------------------------------
    // EMPLOYEE INFORMATION
    // Read-only from self profile
    // ------------------------------------------------------------

    public string EmployeeCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    // ------------------------------------------------------------
    // ORGANIZATION
    // ------------------------------------------------------------

    public int TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public int? BranchId { get; set; }

    public string? BranchName { get; set; }

    // ------------------------------------------------------------
    // ACCOUNT
    // ------------------------------------------------------------

    public bool IsActive { get; set; }
}
