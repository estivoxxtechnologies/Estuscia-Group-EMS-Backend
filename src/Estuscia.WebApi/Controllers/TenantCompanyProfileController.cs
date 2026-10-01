using System.Security.Claims;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
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

    public TenantCompanyProfileController(
        AppDbContext db,
        ICurrentTenantService tenantService)
    {
        _db = db;
        _tenantService = tenantService;
    }

    /* =========================================================
       GET COMPANY DETAILS
       Company Admin only
    ========================================================= */

    [HttpGet]
    public async Task<ActionResult<TenantCompanyProfileDto>> Get()
    {
        if (!IsCompanyAdmin())
            return Forbid();

        var tenantId = GetTenantId();

        if (tenantId == null)
            return BadRequest("Tenant is required.");

        var profile =
            await _db.TenantCompanyProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenantId);

        /*
         * If the tenant does not have a profile yet,
         * return an empty object instead of 404.
         */
        if (profile == null)
        {
            return Ok(
                new TenantCompanyProfileDto
                {
                    TenantId = tenantId.Value
                });
        }

        return Ok(ToDto(profile));
    }

    /* =========================================================
       UPDATE COMPANY DETAILS
       Company Admin only
    ========================================================= */

    [HttpPut]
    public async Task<ActionResult<TenantCompanyProfileDto>> Update(
        [FromBody]
        UpdateTenantCompanyProfileRequest request)
    {
        if (!IsCompanyAdmin())
            return Forbid();

        var tenantId = GetTenantId();

        if (tenantId == null)
            return BadRequest("Tenant is required.");

        var userId = GetUserId();

        var profile =
            await _db.TenantCompanyProfiles
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenantId);

        /* =====================================================
           CREATE
        ===================================================== */

        if (profile == null)
        {
            profile = new TenantCompanyProfile
            {
                TenantId = tenantId.Value,

                CreatedAtUtc =
                    DateTime.UtcNow,

                CreatedByUserId =
                    userId
            };

            _db.TenantCompanyProfiles.Add(profile);
        }

        /* =====================================================
           UPDATE
        ===================================================== */

        profile.LegalName =
            Clean(request.LegalName);

        profile.DisplayName =
            Clean(request.DisplayName);

        profile.AddressLine1 =
            Clean(request.AddressLine1);

        profile.AddressLine2 =
            Clean(request.AddressLine2);

        profile.City =
            Clean(request.City);

        profile.State =
            Clean(request.State);

        profile.PostalCode =
            Clean(request.PostalCode);

        profile.Country =
            Clean(request.Country);

        profile.Phone =
            Clean(request.Phone);

        profile.Email =
            Clean(request.Email);

        profile.Website =
            Clean(request.Website);

        profile.TaxRegistrationNumber =
            Clean(request.TaxRegistrationNumber);

        profile.CompanyRegistrationNumber =
            Clean(request.CompanyRegistrationNumber);

        profile.PayslipFooterText =
            Clean(request.PayslipFooterText);

        profile.UpdatedAtUtc =
            DateTime.UtcNow;

        profile.UpdatedByUserId =
            userId;

        await _db.SaveChangesAsync();

        return Ok(ToDto(profile));
    }

    /* =========================================================
       TENANT
    ========================================================= */

    private int? GetTenantId()
    {
        /*
         * Keep SuperAdmin tenant switching available for
         * tenant context resolution.
         *
         * However, SuperAdmin cannot access this controller
         * because GET/PUT both require company_admin.
         */
        if (_tenantService.IsSuperAdmin)
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

    /* =========================================================
       USER
    ========================================================= */

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

    /* =========================================================
       ROLE
    ========================================================= */

    private bool IsCompanyAdmin()
    {
        return User.IsInRole(
            "company_admin");
    }

    /* =========================================================
       CLEAN VALUE
    ========================================================= */

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    /* =========================================================
       DTO
    ========================================================= */

    private static TenantCompanyProfileDto ToDto(
        TenantCompanyProfile profile)
    {
        return new TenantCompanyProfileDto
        {
            Id = profile.Id,

            TenantId =
                profile.TenantId ?? 0,

            LegalName =
                profile.LegalName,

            DisplayName =
                profile.DisplayName,

            LogoUrl =
                profile.LogoUrl,

            LogoFileName =
                profile.LogoFileName,

            LogoContentType =
                profile.LogoContentType,

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

/* =========================================================
   REQUEST
========================================================= */

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

/* =========================================================
   RESPONSE DTO
========================================================= */

public class TenantCompanyProfileDto
{
    public int Id { get; set; }

    public int TenantId { get; set; }

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