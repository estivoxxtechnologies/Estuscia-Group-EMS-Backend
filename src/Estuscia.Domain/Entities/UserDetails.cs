
using Estuscia.Domain.Common;

namespace Estuscia.Domain.Entities;

public class UserDetails : BaseEntity, IMultiTenantEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public int? TenantId { get; set; }
    public int? BranchId { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public DateTime? DateOfJoining { get; set; }

    public string? PhoneNumber { get; set; }
    public string? OfficialNumber { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactNumber { get; set; }

    public string? BankName { get; set; }
    public string? AccountHolderName { get; set; }

    // Store encrypted account data, never plaintext.
    public string? AccountNumber { get; set; }
    public string? IFSCCode { get; set; }
    public string? BankBranchName { get; set; }

    public string? Notes { get; set; }

    public ApplicationUser? User { get; set; }
}
