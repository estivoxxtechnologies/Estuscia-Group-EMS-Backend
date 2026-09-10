using Estuscia.Domain.Enums;

namespace Estuscia.Application.Common.DTOs.TenantPayment;

public record CreateTenantPaymentDto
(
    int TenantId,
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