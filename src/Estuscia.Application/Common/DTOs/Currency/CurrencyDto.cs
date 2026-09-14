namespace Estuscia.Application.Common.DTOs.Currency;

public record CurrencyDto(
    int Id,
    string Code,
    string Name,
    string Symbol,
    bool IsActive
);