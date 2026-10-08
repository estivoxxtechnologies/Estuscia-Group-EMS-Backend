public class CreateCompanyAdminDto
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string EmployeeCode { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public decimal SalaryBase { get; set; }

    public string? AvatarUrl { get; set; }
}