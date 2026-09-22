namespace Estuscia.Application.Common.DTOs.Tenant;

public record CreateTenantDto(
    string Name,
    string Code,
    string Domain,
    string Plan,
    int DefaultCurrencyId,
    decimal StandardWorkingHours,
    TimeOnly WorkStartTime,
    TimeOnly WorkEndTime,
    bool isActive
);