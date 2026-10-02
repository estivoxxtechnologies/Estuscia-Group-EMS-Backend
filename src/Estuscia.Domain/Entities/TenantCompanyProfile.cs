using Estuscia.Domain.Common;

namespace Estuscia.Domain.Entities;

public class TenantCompanyProfile : BaseEntity, IMultiTenantEntity
{
    public int? TenantId { get; set; }

    public string? LegalName { get; set; }

    public string? DisplayName { get; set; }

    // =========================================================
    // COMPANY LOGO
    // =========================================================

    public string? LogoUrl { get; set; }

    public string? LogoFileName { get; set; }

    public string? LogoContentType { get; set; }

    // =========================================================
    // ADDRESS
    // =========================================================

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    // =========================================================
    // CONTACT
    // =========================================================

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    // =========================================================
    // REGISTRATION
    // =========================================================

    public string? TaxRegistrationNumber { get; set; }

    public string? CompanyRegistrationNumber { get; set; }

    // =========================================================
    // PAYSLIP
    // =========================================================

    public string? PayslipFooterText { get; set; }

    // =========================================================
    // AUDIT
    // =========================================================

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int? CreatedByUserId { get; set; }

    public int? UpdatedByUserId { get; set; }

    // =========================================================
    // RELATIONSHIP
    // =========================================================

    public Tenant? Tenant { get; set; }
}