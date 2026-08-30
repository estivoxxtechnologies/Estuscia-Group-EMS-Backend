using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Estuscia.Infrastructure.Services;

public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || !user.Identity?.IsAuthenticated == true) return null;

            // Allow SuperAdmin to impersonate / switch tenant scope using X-Tenant-Id header
            if (IsSuperAdmin && _httpContextAccessor.HttpContext?.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) == true)
            {
                if (Guid.TryParse(tenantHeader, out var parsedTenantId))
                    return parsedTenantId;
            }

            var tenantClaim = user.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : null;
        }
    }

    public string? BranchName
    {
        get
        {
            if (_httpContextAccessor.HttpContext?.Request.Headers.TryGetValue("X-Branch-Scope", out var branchHeader) == true)
            {
                var branch = branchHeader.ToString();
                if (!string.IsNullOrEmpty(branch) && branch != "All Branches")
                    return branch;
            }
            return _httpContextAccessor.HttpContext?.User.FindFirst("branch")?.Value;
        }
    }

    public bool IsSuperAdmin =>
        _httpContextAccessor.HttpContext?.User.IsInRole("super_admin") ?? false;

    public Guid? UserId
    {
        get
        {
            var idClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }
}

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _config;

    public JwtTokenGenerator(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(ApplicationUser user)
    {
        var secret = _config["Jwt:Secret"] ?? "EstusciaSecretKey_MustBeLongerThan32CharsRequiredForJwt!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString().ToLower()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("branch", user.BranchName),
            new Claim("designation", user.Designation)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(12),
            SigningCredentials = credentials,
            Issuer = _config["Jwt:Issuer"] ?? "https://api.estuscia.com",
            Audience = _config["Jwt:Audience"] ?? "https://portal.estuscia.com"
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
