using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Estuscia.Domain.Enums;

namespace Estuscia.Infrastructure.Services;

// ============================================================
// CURRENT TENANT / USER CONTEXT
// ============================================================
//
// SECURITY RULE:
//
// TenantId, UserId, Role and SuperAdmin status come ONLY from
// the validated JWT.
//
// Do NOT trust:
//   X-Tenant-Id
//   X-Branch-Scope
//   tenantId from query string
//   tenantId from request body
//
// A SuperAdmin should use an explicit server-side operation if
// tenant switching/impersonation is required.
// ============================================================

public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User =>
        _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true;

    // ========================================================
    // USER ID
    // ========================================================

    public Guid? UserId
    {
        get
        {
            if (!IsAuthenticated)
                return null;

            var value =
                User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value
                ?? User?.FindFirst("user_id")?.Value;

            return Guid.TryParse(value, out var id)
                ? id
                : null;
        }
    }

    // ========================================================
    // TENANT ID
    // ========================================================

    public Guid? TenantId
    {
        get
        {
            if (!IsAuthenticated)
                return null;

            var value =
                User?.FindFirst("tenant_id")?.Value;

            return Guid.TryParse(value, out var tenantId)
                ? tenantId
                : null;
        }
    }

    // ========================================================
    // BRANCH
    // ========================================================
    //
    // Branch is read from the authenticated JWT.
    //
    // We deliberately DO NOT read X-Branch-Scope from the
    // request because the client can modify request headers.
    //
    // "All Branches" should be represented by null in the JWT
    // rather than a magic string.
    // ========================================================

    public string? BranchName
    {
        get
        {
            if (!IsAuthenticated)
                return null;

            var branch =
                User?.FindFirst("branch")?.Value;

            if (string.IsNullOrWhiteSpace(branch))
                return null;

            return branch;
        }
    }

    // ========================================================
    // SUPER ADMIN
    // ========================================================

    public bool IsSuperAdmin
    {
        get
        {
            if (!IsAuthenticated)
                return false;

            return User?.IsInRole("super_admin") == true;
        }
    }
}


// ============================================================
// JWT TOKEN GENERATOR
// ============================================================

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _config;

    public JwtTokenGenerator(IConfiguration config)
    {
        _config = config;
    }
    private static string GetRoleName(UserRole role)
    {
        return role switch
        {
            UserRole.SuperAdmin => "super_admin",
            UserRole.CompanyAdmin => "company_admin",
            UserRole.HrOps => "hr_ops",
            UserRole.BranchManager => "branch_manager",
            UserRole.SalesStaff => "sales_staff",
            UserRole.Developer => "developer",
            UserRole.SupportStaff => "support_staff",
            UserRole.KnowledgeTrainer => "knowledge_trainer",

            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "Unsupported user role.")
        };
    }

    public string GenerateToken(ApplicationUser user)
    {
        var secret = _config["Jwt:Secret"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "Jwt:Secret is not configured.");
        }

        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Secret must contain at least 32 bytes.");
        }

        var issuer =
            _config["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "Jwt:Issuer is not configured.");

        var audience =
            _config["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "Jwt:Audience is not configured.");

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secret));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
{
    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),

    new Claim("user_id", user.Id.ToString()),

    new Claim("nameid", user.Id.ToString()),

    new Claim("email", user.Email),

    new Claim("unique_name", user.FullName),

    new Claim("role", GetRoleName(user.Role)),

    new Claim("tenant_id", user.TenantId.ToString()),

    new Claim("branch_id", user.BranchId.ToString()),

    new Claim("designation", user.Designation ?? string.Empty)
};

        var tokenDescriptor =
            new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),

                Expires =
                    DateTime.UtcNow.AddHours(12),

                NotBefore =
                    DateTime.UtcNow,

                IssuedAt =
                    DateTime.UtcNow,

                SigningCredentials =
                    credentials,

                Issuer = issuer,

                Audience = audience
            };

        var tokenHandler =
            new JwtSecurityTokenHandler();

        var token =
            tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    // ========================================================
    // REFRESH TOKEN
    // ========================================================

    public string GenerateRefreshToken()
    {
        var randomNumber =
            new byte[32];

        using var rng =
            RandomNumberGenerator.Create();

        rng.GetBytes(randomNumber);

        return Convert.ToBase64String(
            randomNumber);
    }
}
