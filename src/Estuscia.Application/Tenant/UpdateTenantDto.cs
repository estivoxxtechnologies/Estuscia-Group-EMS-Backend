namespace Estuscia.Application.Common.DTOs.Tenant;

public record UpdateTenantDto(
    string Name,
    string Code,
    string Domain,
    string Plan,
    string Currency,
    bool isActive
);