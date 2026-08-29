using Estuscia.Domain.Enums;

namespace Estuscia.Application.Common.DTOs;

public record LoginRequestDto(string Email, string Password);

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string Name,
    string Role,
    string Designation,
    string Branch,
    Guid TenantId,
    string TenantName,
    string Avatar
);

public record SubmitWorkLogDto(
    DateOnly? WorkDate,
    WorkLogType WorkType,
    string Narration,
    int? CallsMade,
    int? CallsConnected,
    int? LeadsRespondedWell,
    int? FollowUpsScheduled,
    decimal? HoursSpent,
    string? FeaturesShipped,
    string? RepositoryPrLinks,
    string? BlockersEncountered
);

public record IssueReceiptDto(
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    decimal DepositAmount,
    string SlabTierName,
    decimal AnnualYieldPercent,
    int LockinPeriodMonths,
    string PaymentMode,
    string BankReferenceNumber,
    string PayoutFrequency
);

public record CreateTenantDto(
    string Name,
    string Code,
    string Domain,
    string Plan,
    string Currency,
    List<string> Branches
);
