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

    public CurrencyDto? Currency { get; set; }
}