namespace Estuscia.Application.Branches.DTOs;

public class UpdateBranchDto
{
    public string BranchName { get; set; } = string.Empty;
    public string? City { get; set; }
    public bool IsActive { get; set; }
    public int CurrencyId { get; set; }

    public decimal? StandardWorkingHours { get; set; }

    public TimeOnly? WorkStartTime { get; set; }

    public TimeOnly? WorkEndTime { get; set; }
}