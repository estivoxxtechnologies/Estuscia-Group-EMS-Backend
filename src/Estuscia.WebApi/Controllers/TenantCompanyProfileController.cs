using System.Security.Claims;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantCompanyProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantService _tenantService;
    private readonly IWebHostEnvironment _environment;

    private const long MaxLogoSizeBytes = 2 * 1024 * 1024; // 2 MB

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
        IWebHostEnvironment environment)
    {
        _db = db;
        _tenantService = tenantService;
        _environment = environment;
    }

    // ============================================================
    // GET COMPANY PROFILE
    // ============================================================

    [HttpGet]
    public async Task<ActionResult<TenantCompanyProfileDto>> Get()
    {
        if (!IsCompanyAdmin())
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

        // --------------------------------------------------------
        // Validate extension
        // --------------------------------------------------------

        var extension = Path
            .GetExtension(logo.FileName)
            .ToLowerInvariant();

        if (!AllowedLogoExtensions.Contains(extension))
        {
            return BadRequest(
                "Only PNG, JPG, JPEG and WEBP logo files are allowed.");
        }

        // --------------------------------------------------------
        // Validate content type
        // --------------------------------------------------------

        var contentType =
            logo.ContentType?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(contentType) ||
            !AllowedLogoContentTypes.Contains(contentType))
        {
            return BadRequest(
                "Invalid logo content type.");
        }

        // --------------------------------------------------------
        // Get tenant
        // --------------------------------------------------------

        var tenantId = GetTenantId();

        if (!tenantId.HasValue)
            return BadRequest(
                "Tenant could not be determined.");

        var userId = GetUserId();

        // --------------------------------------------------------
        // Find existing profile
        // --------------------------------------------------------

        var profile = await _db.TenantCompanyProfiles
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId.Value);

        // --------------------------------------------------------
        // Create profile if it doesn't exist
        // --------------------------------------------------------

        if (profile == null)
        {
            profile =
                new Domain.Entities.TenantCompanyProfile
                {
                    TenantId = tenantId.Value,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

            _db.TenantCompanyProfiles.Add(profile);
        }

        // --------------------------------------------------------
        // Delete previous physical logo
        // --------------------------------------------------------

        DeleteExistingLogo(profile.LogoUrl);

        // --------------------------------------------------------
        // Resolve wwwroot
        // --------------------------------------------------------

        var webRoot =
            _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(
                _environment.ContentRootPath,
                "wwwroot");
        }

        // --------------------------------------------------------
        // Create tenant-specific directory
        // --------------------------------------------------------

        var uploadsRoot = Path.Combine(
            webRoot,
            "uploads",
            "company-logos",
            $"tenant-{tenantId.Value}"
        );

        Directory.CreateDirectory(uploadsRoot);

        // --------------------------------------------------------
        // Generate safe filename
        // --------------------------------------------------------

        var generatedFileName =
            $"logo-{Guid.NewGuid():N}{extension}";

        var physicalPath =
            Path.Combine(
                uploadsRoot,
                generatedFileName);

        // --------------------------------------------------------
        // Save uploaded file
        // --------------------------------------------------------

        await using (
            var stream = new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
        {
            await logo.CopyToAsync(stream);
        }

        // --------------------------------------------------------
        // Generate public URL
        // --------------------------------------------------------

        var logoUrl =
            $"{Request.Scheme}://{Request.Host}" +
            $"/uploads/company-logos/tenant-{tenantId.Value}" +
            $"/{generatedFileName}";

        // --------------------------------------------------------
        // Update database
        // --------------------------------------------------------

        profile.LogoUrl = logoUrl;

        profile.LogoFileName =
            Path.GetFileName(logo.FileName);

        profile.LogoContentType =
            contentType;

        profile.UpdatedAtUtc =
            DateTime.UtcNow;

        profile.UpdatedByUserId =
            userId;

        await _db.SaveChangesAsync();

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
            return BadRequest(
                "Tenant could not be determined.");

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

        // --------------------------------------------------------
        // Delete physical file
        // --------------------------------------------------------

        DeleteExistingLogo(profile.LogoUrl);

        // --------------------------------------------------------
        // Clear logo information
        // --------------------------------------------------------

        profile.LogoUrl = null;
        profile.LogoFileName = null;
        profile.LogoContentType = null;

        profile.UpdatedAtUtc =
            DateTime.UtcNow;

        profile.UpdatedByUserId =
            userId;

        await _db.SaveChangesAsync();

        return Ok(ToDto(profile));
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private bool IsCompanyAdmin()
    {
        return User.IsInRole("company_admin");
    }

    // ------------------------------------------------------------
    // Tenant ID
    // ------------------------------------------------------------

    private int? GetTenantId()
    {
        if (User.IsInRole("super_admin"))
        {
            var headerTenantId =
                Request.Headers["X-Tenant-Id"]
                    .FirstOrDefault();

            if (int.TryParse(
                headerTenantId,
                out var parsedTenantId))
            {
                return parsedTenantId;
            }
        }

        return _tenantService.TenantId;
    }

    // ------------------------------------------------------------
    // Current User ID
    // ------------------------------------------------------------

    private int? GetUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        return int.TryParse(
            value,
            out var userId)
            ? userId
            : null;
    }

    // ------------------------------------------------------------
    // Clean string
    // ------------------------------------------------------------

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    // ------------------------------------------------------------
    // Delete existing logo
    // ------------------------------------------------------------

    private void DeleteExistingLogo(
        string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return;

        try
        {
            var uri = new Uri(logoUrl);

            var relativePath =
                uri.AbsolutePath
                    .TrimStart('/')
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar);

            var webRoot =
                _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot");
            }

            var physicalPath =
                Path.Combine(
                    webRoot,
                    relativePath);

            if (System.IO.File.Exists(
                physicalPath))
            {
                System.IO.File.Delete(
                    physicalPath);
            }
        }
        catch
        {
            // Do not fail database operations
            // if the old physical logo cannot
            // be deleted.
        }
    }

    // ============================================================
    // ENTITY -> DTO
    // ============================================================

    private static TenantCompanyProfileDto ToDto(
        Domain.Entities.TenantCompanyProfile profile)
    {
        return new TenantCompanyProfileDto
        {
            Id = profile.Id,
            TenantId = profile.TenantId,

            LegalName = profile.LegalName,
            DisplayName = profile.DisplayName,

            LogoUrl = profile.LogoUrl,
            LogoFileName = profile.LogoFileName,
            LogoContentType = profile.LogoContentType,

            AddressLine1 =
                profile.AddressLine1,

            AddressLine2 =
                profile.AddressLine2,

            City =
                profile.City,

            State =
                profile.State,

            PostalCode =
                profile.PostalCode,

            Country =
                profile.Country,

            Phone =
                profile.Phone,

            Email =
                profile.Email,

            Website =
                profile.Website,

            TaxRegistrationNumber =
                profile.TaxRegistrationNumber,

            CompanyRegistrationNumber =
                profile.CompanyRegistrationNumber,

            PayslipFooterText =
                profile.PayslipFooterText,

            CreatedAtUtc =
                profile.CreatedAtUtc,

            UpdatedAtUtc =
                profile.UpdatedAtUtc,

            CreatedByUserId =
                profile.CreatedByUserId,

            UpdatedByUserId =
                profile.UpdatedByUserId
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