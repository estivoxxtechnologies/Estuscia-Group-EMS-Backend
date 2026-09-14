using Estuscia.Application.Common.DTOs;
using Estuscia.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAppDbContext _context;
    private readonly IJwtTokenGenerator _jwtGenerator;

    public AuthController(
        IAppDbContext context,
        IJwtTokenGenerator jwtGenerator)
    {
        _context = context;
        _jwtGenerator = jwtGenerator;
    }

    // ============================================================
    // LOGIN
    // ============================================================

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request)
    {
        // ============================================================
        // VALIDATE REQUEST
        // ============================================================

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Email and password are required."
            });
        }

        var email = request.Email
            .Trim()
            .ToLowerInvariant();

        // ============================================================
        // FIND USER
        //
        // Currency relationships are loaded here:
        // - Tenant.DefaultCurrency
        // - Branch.Currency
        // ============================================================

        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
                .ThenInclude(t => t.DefaultCurrency)
            .Include(u => u.Branch)
                .ThenInclude(b => b.Currency)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(
                u => u.Email.ToLower() == email);

        // ============================================================
        // INVALID USER
        // ============================================================

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        // ============================================================
        // ACCOUNT STATUS
        // ============================================================

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message =
                    "Your account is inactive. Please contact an administrator."
            });
        }

        // ============================================================
        // PASSWORD VERIFICATION
        // ============================================================

        if (!BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        // ============================================================
        // TENANT VALIDATION
        // ============================================================

        if (user.Tenant == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your account is not associated with an organization."
            });
        }

        if (!user.Tenant.IsActive)
        {
            return Unauthorized(new
            {
                message = "This organization is inactive."
            });
        }

        // ============================================================
        // TENANT CURRENCY VALIDATION
        // ============================================================

        if (user.Tenant.DefaultCurrency == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your organization does not have a valid default currency configured."
            });
        }

        // ============================================================
        // ROLE VALIDATION
        // ============================================================

        if (user.Role == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your account does not have a valid role assigned."
            });
        }

        if (!user.Role.IsActive)
        {
            return Unauthorized(new
            {
                message =
                    "Your assigned role is inactive. Please contact an administrator."
            });
        }

        // ============================================================
        // BRANCH VALIDATION
        //
        // Current application model requires users to have a branch.
        // ============================================================

        if (!user.BranchId.HasValue || user.Branch == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your account is not associated with a valid branch."
            });
        }

        if (!user.Branch.IsActive)
        {
            return Unauthorized(new
            {
                message =
                    "Your branch is inactive. Please contact an administrator."
            });
        }

        // ============================================================
        // BRANCH TENANT VALIDATION
        // ============================================================

        if (user.Branch.TenantId != user.TenantId)
        {
            return Unauthorized(new
            {
                message =
                    "Your account has an invalid branch configuration."
            });
        }

        // ============================================================
        // BRANCH CURRENCY VALIDATION
        // ============================================================

        if (user.Branch.Currency == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your branch does not have a valid currency configured."
            });
        }

        // ============================================================
        // GENERATE JWT
        // ============================================================

        var accessToken =
            _jwtGenerator.GenerateToken(user);

        var refreshToken =
            _jwtGenerator.GenerateRefreshToken();

        // ============================================================
        // RESPONSE
        // ============================================================

        var response = new
        {
            accessToken,
            refreshToken,

            user = new
            {
                userId = user.Id,
                username = user.FullName,
                email = user.Email,

                roleId = user.RoleNumber,
                roleName = user.Role.RoleName,

                designation = user.Designation,

                tenantId = user.TenantId,
                tenantName = user.Tenant.Name,

                branchId = user.BranchId,
                branchName = user.Branch.BranchName,

                avatarUrl = user.AvatarUrl,

                tenantCurrency = new
                {
                    id = user.Tenant.DefaultCurrency.Id,
                    code = user.Tenant.DefaultCurrency.Code,
                    name = user.Tenant.DefaultCurrency.Name,
                    symbol = user.Tenant.DefaultCurrency.Symbol
                },

                branchCurrency = new
                {
                    id = user.Branch.Currency.Id,
                    code = user.Branch.Currency.Code,
                    name = user.Branch.Currency.Name,
                    symbol = user.Branch.Currency.Symbol
                }
            }
        };

        return Ok(response);
    }

    // ============================================================
    // CURRENT USER
    // ============================================================

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        // ============================================================
        // USER ID FROM JWT
        // ============================================================

        var userIdClaim =
            User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(
                System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("user_id")?.Value;

        // Current IDs are INT, not GUID.
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        // ============================================================
        // LOAD USER + REQUIRED RELATIONSHIPS
        //
        // Currency relationships:
        // - Tenant.DefaultCurrency
        // - Branch.Currency
        // ============================================================

        var user = await _context.Users
            .Include(u => u.Tenant)
                .ThenInclude(t => t.DefaultCurrency)
            .Include(u => u.Branch)
                .ThenInclude(b => b.Currency)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return Unauthorized();
        }

        // ============================================================
        // ACCOUNT STATUS
        // ============================================================

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "Your account is inactive."
            });
        }

        // ============================================================
        // TENANT
        // ============================================================

        if (user.Tenant == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your account is not associated with an organization."
            });
        }

        if (!user.Tenant.IsActive)
        {
            return Unauthorized(new
            {
                message = "This organization is inactive."
            });
        }

        // ============================================================
        // TENANT CURRENCY
        // ============================================================

        if (user.Tenant.DefaultCurrency == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your organization does not have a valid default currency configured."
            });
        }

        // ============================================================
        // ROLE
        // ============================================================

        if (user.Role == null || !user.Role.IsActive)
        {
            return Unauthorized(new
            {
                message =
                    "Your account does not have a valid active role."
            });
        }

        // ============================================================
        // BRANCH
        // ============================================================

        if (!user.BranchId.HasValue || user.Branch == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your account is not associated with a valid branch."
            });
        }

        if (!user.Branch.IsActive)
        {
            return Unauthorized(new
            {
                message = "Your branch is inactive."
            });
        }

        // ============================================================
        // BRANCH TENANT VALIDATION
        // ============================================================

        if (user.Branch.TenantId != user.TenantId)
        {
            return Unauthorized(new
            {
                message =
                    "Your account has an invalid branch configuration."
            });
        }

        // ============================================================
        // BRANCH CURRENCY
        // ============================================================

        if (user.Branch.Currency == null)
        {
            return Unauthorized(new
            {
                message =
                    "Your branch does not have a valid currency configured."
            });
        }

        // ============================================================
        // RESPONSE
        // ============================================================

        var response = new
        {
            userId = user.Id,
            username = user.FullName,
            email = user.Email,

            roleId = user.RoleNumber,
            roleName = user.Role.RoleName,

            designation = user.Designation,

            tenantId = user.TenantId,
            tenantName = user.Tenant.Name,

            branchId = user.BranchId,
            branchName = user.Branch.BranchName,

            avatarUrl = user.AvatarUrl,

            tenantCurrency = new
            {
                id = user.Tenant.DefaultCurrency.Id,
                code = user.Tenant.DefaultCurrency.Code,
                name = user.Tenant.DefaultCurrency.Name,
                symbol = user.Tenant.DefaultCurrency.Symbol
            },

            branchCurrency = new
            {
                id = user.Branch.Currency.Id,
                code = user.Branch.Currency.Code,
                name = user.Branch.Currency.Name,
                symbol = user.Branch.Currency.Symbol
            }
        };

        return Ok(response);
    }
}