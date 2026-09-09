namespace Estuscia.Application.Branches.DTOs;

public class UpdateBranchDto
{
    public string BranchName { get; set; } = string.Empty;

    public string? City { get; set; }

    public bool IsActive { get; set; }
}