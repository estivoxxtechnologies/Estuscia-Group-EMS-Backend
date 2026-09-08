namespace Estuscia.Application.Common.DTOs.Tenant;

public record TenantResponseDto(
    Guid Id,
    string Name,
    string Code,
    string Domain,
    string Plan,
    string Currency,
    bool isActive,
    DateTime CreatedAtUtc
);