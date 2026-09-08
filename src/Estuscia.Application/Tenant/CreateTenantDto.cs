namespace Estuscia.Application.Common.DTOs.Tenant;

public record CreateTenantDto(
    string Name,
    string Code,
    string Domain,
    string Plan,
    string Currency,
    bool isActive
);