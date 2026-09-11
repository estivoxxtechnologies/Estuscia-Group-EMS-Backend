using Estuscia.Domain.Enums;

namespace Estuscia.Application.Common.DTOs.TenantPayment;

public record UpdateTenantPaymentDto(
    int TotalBranches,
    PaymentMode PaymentMode,
    decimal Amount,
    PaymentStatus PaymentStatus,
    DateTime? PaymentDateUtc,
    DateTime? ValidFromUtc,
    DateTime? ValidUntilUtc,
    bool RegistrationStatus,
    string? Notes
);