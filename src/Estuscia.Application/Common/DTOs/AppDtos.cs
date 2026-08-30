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
// TENANT / COMPANY ONBOARDING
// ============================================================

public record CreateTenantDto(
    string Name,
    string Code,
    string Domain,
    string Plan,
    string Currency,
    List<string> Branches
);

// ============================================================
// EMPLOYEE CREATION / ONBOARDING
// ============================================================

public record CreateUserDto(
    Guid TenantId,
    string BranchName,
    string FullName,
    string Email,
    string Password,
    string EmployeeCode,
    UserRole Role,
    string Designation,
    string Department,
    decimal SalaryBase,
    string? AvatarUrl
);

// ============================================================
// UPDATE EMPLOYEE
// ============================================================

public record UpdateUserDto(
    string BranchName,
    string FullName,
    string Email,
    string EmployeeCode,
    UserRole Role,
    string Designation,
    string Department,
    decimal SalaryBase,
    string? AvatarUrl,
    bool IsActive
);

// ============================================================
// CHANGE PASSWORD
// ============================================================

public record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword
);