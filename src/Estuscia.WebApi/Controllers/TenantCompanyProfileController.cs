
using System.Security.Claims;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Estuscia.WebApi.Security;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantCompanyProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _tenantService;
    private readonly IFileStorageService _fileStorage;

    private const long MaxLogoSizeBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedLogoContentTypes =
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    private static readonly string[] AllowedLogoExtensions =
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    public TenantCompanyProfileController(
        AppDbContext db,
        ICurrentTenantService tenantService,
        IFileStorageService fileStorage)
    {
        _db = db;
        _tenantService = tenantService;
        _fileStorage = fileStorage;
    }

    // ============================================================
    // GET COMPANY PROFILE
    // ============================================================

    [HttpGet]
    public async Task<ActionResult<TenantCompanyProfileDto>> Get()
    {
        if (!CanViewCompanyProfile())
            return Forbid();

        var tenantId = GetTenantId();

        if (!tenantId.HasValue)
            return BadRequest("Tenant could not be determined.");

        var profile = await _db.TenantCompanyProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value);

        if (profile == null)
        {
            return Ok(new TenantCompanyProfileDto
            {
                TenantId = tenantId.Value
            });
        }

        return Ok(ToDto(profile));
    }

    // ============================================================
    // UPDATE COMPANY PROFILE
    // ============================================================

    [HttpPut]
    public async Task<ActionResult<TenantCompanyProfileDto>> Update(
        [FromBody] UpdateTenantCompanyProfileRequest request)
    {
        if (!IsCompanyAdmin())
            return Forbid();

        var tenantId = GetTenantId();

        if (!tenantId.HasValue)
            return BadRequest("Tenant could not be determined.");

        var userId = GetUserId();

        var profile = await _db.TenantCompanyProfiles
            .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value);

        if (profile == null)
        {
            profile = new Domain.Entities.TenantCompanyProfile
            {
                TenantId = tenantId.Value,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            _db.TenantCompanyProfiles.Add(profile);
        }

        profile.LegalName = Clean(request.LegalName);
        profile.DisplayName = Clean(request.DisplayName);

        profile.AddressLine1 = Clean(request.AddressLine1);
        profile.AddressLine2 = Clean(request.AddressLine2);
        profile.City = Clean(request.City);
        profile.State = Clean(request.State);
        profile.PostalCode = Clean(request.PostalCode);
        profile.Country = Clean(request.Country);

        profile.Phone = Clean(request.Phone);
        profile.Email = Clean(request.Email);
        profile.Website = Clean(request.Website);

        profile.TaxRegistrationNumber =
            Clean(request.TaxRegistrationNumber);

        profile.CompanyRegistrationNumber =
            Clean(request.CompanyRegistrationNumber);

        profile.PayslipFooterText =
            Clean(request.PayslipFooterText);

        profile.UpdatedAtUtc = DateTime.UtcNow;
        profile.UpdatedByUserId = userId;

        await _db.SaveChangesAsync();

        return Ok(ToDto(profile));
    }

    // ============================================================
    // UPLOAD COMPANY LOGO
    // ============================================================

    [HttpPost("logo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxLogoSizeBytes + 1024 * 1024)]
    public async Task<ActionResult<TenantCompanyProfileDto>> UploadLogo(
        [FromForm] CompanyLogoUploadRequest request)
    {
        if (!IsCompanyAdmin())
            return Forbid();

        var logo = request.Logo;

        if (logo == null || logo.Length == 0)
            return BadRequest("Please select a logo file.");

        if (logo.Length > MaxLogoSizeBytes)
            return BadRequest("Logo size cannot exceed 2 MB.");

        var extension = Path
            .GetExtension(logo.FileName)
            .ToLowerInvariant();

        if (!AllowedLogoExtensions.Contains(extension))
        {
            return BadRequest(
                "Only PNG, JPG, JPEG and WEBP logo files are allowed.");
        }

        if (!await UploadedFileSignatureValidator.IsValidAsync(
        logo, extension, HttpContext.RequestAborted))
        {
            return BadRequest("The uploaded file is not a valid image.");
        }

        var contentType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException("Unsupported logo format.")
        };

        if (string.IsNullOrWhiteSpace(contentType) ||
            !AllowedLogoContentTypes.Contains(contentType))
        {
            return BadRequest("Invalid logo content type.");
        }

        var tenantId = GetTenantId();

        if (!tenantId.HasValue)
            return BadRequest("Tenant could not be determined.");

        var userId = GetUserId();

        var profile = await _db.TenantCompanyProfiles
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId.Value);

        if (profile == null)
        {
            profile = new Domain.Entities.TenantCompanyProfile
            {
                TenantId = tenantId.Value,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            _db.TenantCompanyProfiles.Add(profile);
        }

        // Preserve the old logo until the new logo is saved.
        var previousLogoUrl = profile.LogoUrl;

        var generatedFileName =
            $"logo-{Guid.NewGuid():N}{extension}";

        var storageKey =
            $"uploads/company-logos/tenant-{tenantId.Value}/{generatedFileName}";

        // Upload the new logo to private S3.
        await using (var stream = logo.OpenReadStream())
        {
            await _fileStorage.UploadAsync(
                storageKey,
                stream,
                contentType,
                HttpContext.RequestAborted);
        }

        var logoUrl =
            $"{Request.Scheme}://{Request.Host}" +
            $"/uploads/company-logos/tenant-{tenantId.Value}" +
            $"/{generatedFileName}";

        profile.LogoUrl = logoUrl;
        profile.LogoFileName = Path.GetFileName(logo.FileName);
        profile.LogoContentType = contentType;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        profile.UpdatedByUserId = userId;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Remove the new S3 object if the database save fails.
            try
            {
                await _fileStorage.DeleteAsync(storageKey);
            }
            catch
            {
                // Preserve the original database exception.
            }

            throw;
        }

        // Delete the old logo only after the new one is saved.
        if (!string.IsNullOrWhiteSpace(previousLogoUrl))
        {
            var previousKey = GetLogoStorageKey(
                previousLogoUrl,
                tenantId.Value);

            if (previousKey != null &&
                !previousKey.Equals(
                    storageKey,
                    StringComparison.Ordinal))
            {
                try
                {
                    await _fileStorage.DeleteAsync(previousKey);
                }
                catch
                {
                    // A cleanup failure must not undo a successful upload.
                }
            }
        }

        return Ok(ToDto(profile));
    }

    // ============================================================
    // DELETE COMPANY LOGO
    // ============================================================

    [HttpDelete("logo")]
    public async Task<ActionResult<TenantCompanyProfileDto>> DeleteLogo()
    {
        if (!IsCompanyAdmin())
            return Forbid();

        var tenantId = GetTenantId();

        if (!tenantId.HasValue)
            return BadRequest("Tenant could not be determined.");

        var userId = GetUserId();

        var profile = await _db.TenantCompanyProfiles
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId.Value);

        if (profile == null)
        {
            return Ok(new TenantCompanyProfileDto
            {
                TenantId = tenantId.Value
            });
        }

        var previousLogoUrl = profile.LogoUrl;

        // Clear the database first.
        profile.LogoUrl = null;
        profile.LogoFileName = null;
        profile.LogoContentType = null;

        profile.UpdatedAtUtc = DateTime.UtcNow;
        profile.UpdatedByUserId = userId;

        await _db.SaveChangesAsync();

        // Delete the S3 object after the database save.
        if (!string.IsNullOrWhiteSpace(previousLogoUrl))
        {
            var previousKey = GetLogoStorageKey(
                previousLogoUrl,
                tenantId.Value);

            if (previousKey != null)
            {
                try
                {
                    await _fileStorage.DeleteAsync(previousKey);
                }
                catch
                {
                    // Do not fail the profile deletion because of cleanup.
                }
            }
        }

        return Ok(ToDto(profile));
    }

    // ============================================================
    // AUTHORIZATION HELPERS
    // ============================================================

    private bool IsCompanyAdmin()
    {
        return User.IsInRole("company_admin");
    }

    private bool CanViewCompanyProfile()
    {
        return User.IsInRole("super_admin")
            || User.IsInRole("company_admin")
            || User.IsInRole("hr_ops")
            || User.IsInRole("branch_manager")
            || User.IsInRole("sales_staff")
            || User.IsInRole("developer")
            || User.IsInRole("support_staff")
            || User.IsInRole("knowledge_trainer");
    }

    // ============================================================
    // TENANT ID
    // ============================================================

    private int? GetTenantId()
    {
        if (User.IsInRole("super_admin"))
        {
            var headerTenantId =
                Request.Headers["X-Tenant-Id"].FirstOrDefault();

            if (int.TryParse(
                headerTenantId,
                out var parsedTenantId))
            {
                return parsedTenantId;
            }
        }

        return _tenantService.TenantId;
    }

    // ============================================================
    // CURRENT USER ID
    // ============================================================

    private int? GetUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var userId)
            ? userId
            : null;
    }

    // ============================================================
    // CLEAN STRING
    // ============================================================

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    // ============================================================
    // GET S3 STORAGE KEY FROM EXISTING LOGO URL
    // ============================================================

    private static string? GetLogoStorageKey(
        string? logoUrl,
        int tenantId)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return null;

        var path = logoUrl;

        if (Uri.TryCreate(
            logoUrl,
            UriKind.Absolute,
            out var uri))
        {
            path = uri.AbsolutePath;
        }

        const string prefix = "/uploads/company-logos/";

        if (!path.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var key = path.TrimStart('/').Replace('\\', '/');
        var segments = key.Split('/');

        // Expected:
        // uploads/company-logos/tenant-{id}/{filename}

        if (segments.Length != 4 ||
            !segments[0].Equals(
                "uploads",
                StringComparison.OrdinalIgnoreCase) ||
            !segments[1].Equals(
                "company-logos",
                StringComparison.OrdinalIgnoreCase) ||
            !segments[2].Equals(
                $"tenant-{tenantId}",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(segments[3]) ||
            segments[3] != Path.GetFileName(segments[3]))
        {
            return null;
        }

        return key;
    }

    // ============================================================
    // GENERATE PRIVATE S3 DISPLAY URL
    // ============================================================

    private string? GetLogoDisplayUrl(
        string? logoUrl,
        int tenantId)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return null;

        var key = GetLogoStorageKey(logoUrl, tenantId);

        // Preserve external or legacy URLs that are not S3 keys.
        if (key == null)
            return logoUrl;

        return _fileStorage.GetPresignedUrl(
            key,
            TimeSpan.FromMinutes(15));
    }

    // ============================================================
    // ENTITY -> DTO
    // ============================================================

    private TenantCompanyProfileDto ToDto(
        Domain.Entities.TenantCompanyProfile profile)
    {
        return new TenantCompanyProfileDto
        {
            Id = profile.Id,
            TenantId = profile.TenantId,

            LegalName = profile.LegalName,
            DisplayName = profile.DisplayName,

            LogoUrl = GetLogoDisplayUrl(
                profile.LogoUrl,
                profile.TenantId ?? 0),

            LogoFileName = profile.LogoFileName,
            LogoContentType = profile.LogoContentType,

            AddressLine1 = profile.AddressLine1,
            AddressLine2 = profile.AddressLine2,
            City = profile.City,
            State = profile.State,
            PostalCode = profile.PostalCode,
            Country = profile.Country,

            Phone = profile.Phone,
            Email = profile.Email,
            Website = profile.Website,

            TaxRegistrationNumber =
                profile.TaxRegistrationNumber,

            CompanyRegistrationNumber =
                profile.CompanyRegistrationNumber,

            PayslipFooterText =
                profile.PayslipFooterText,

            CreatedAtUtc = profile.CreatedAtUtc,
            UpdatedAtUtc = profile.UpdatedAtUtc,

            CreatedByUserId = profile.CreatedByUserId,
            UpdatedByUserId = profile.UpdatedByUserId
        };
    }
}


// ================================================================
// LOGO UPLOAD REQUEST
// ================================================================

public class CompanyLogoUploadRequest
{
    public IFormFile? Logo { get; set; }
}


// ================================================================
// UPDATE REQUEST
// ================================================================

public class UpdateTenantCompanyProfileRequest
{
    public string? LegalName { get; set; }

    public string? DisplayName { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? TaxRegistrationNumber { get; set; }

    public string? CompanyRegistrationNumber { get; set; }

    public string? PayslipFooterText { get; set; }
}


// ================================================================
// RESPONSE DTO
// ================================================================

public class TenantCompanyProfileDto
{
    public int Id { get; set; }

    public int? TenantId { get; set; }

    public string? LegalName { get; set; }

    public string? DisplayName { get; set; }

    public string? LogoUrl { get; set; }

    public string? LogoFileName { get; set; }

    public string? LogoContentType { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? TaxRegistrationNumber { get; set; }

    public string? CompanyRegistrationNumber { get; set; }

    public string? PayslipFooterText { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int? CreatedByUserId { get; set; }

    public int? UpdatedByUserId { get; set; }
}
