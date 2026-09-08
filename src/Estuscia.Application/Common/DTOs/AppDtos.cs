using Estuscia.Domain.Enums;

namespace Estuscia.Application.Common.DTOs;

// ============================================================
// AUTHENTICATION
// ============================================================

public record LoginRequestDto(
    string Email,
    string Password
);

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    UserDto User
);

// ============================================================
// AUTHENTICATED USER
// ============================================================

public record UserDto(
    int Id,
    string Email,
    string Name,
    string Role,
    string Designation,
    int BranchId,
    string Branch,
    int TenantId,
    string TenantName,
    string Avatar
);

// ============================================================
// WORK LOG
// ============================================================

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

// ============================================================
// CUSTOMER RECEIPT
// ============================================================

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

// ============================================================
// CHANGE PASSWORD
// ============================================================

public record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword
);