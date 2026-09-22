using Estuscia.Application.Common.DTOs.Currency;

namespace Estuscia.Application.Branches.DTOs;

public class BranchDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public string BranchName { get; set; } = string.Empty;
    public string? City { get; set; }
    public bool IsActive { get; set; }

    public int CurrencyId { get; set; }

    // Branch overrides
    public decimal? StandardWorkingHours { get; set; }
    public TimeOnly? WorkStartTime { get; set; }
    public TimeOnly? WorkEndTime { get; set; }

    // Effective values after tenant inheritance
    public decimal EffectiveStandardWorkingHours { get; set; }
    public TimeOnly EffectiveWorkStartTime { get; set; }
    public TimeOnly EffectiveWorkEndTime { get; set; }

    public CurrencyDto? Currency { get; set; }
}