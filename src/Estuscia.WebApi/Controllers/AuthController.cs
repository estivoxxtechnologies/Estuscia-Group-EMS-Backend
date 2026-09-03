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
        // We don't know the tenant before authentication,
        // so IgnoreQueryFilters() is required here.
        // ============================================================

        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .Include(u => u.Branch)
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

        // Extra safety:
        // Ensure branch belongs to the same tenant.

        if (user.Branch.TenantId != user.TenantId)
        {
            return Unauthorized(new
            {
                message =
                    "Your account has an invalid branch configuration."
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
        //
        // Role comes directly from the database Role entity.
        // ============================================================

        var response = new AuthResponseDto(
            accessToken,
            refreshToken,
            new UserDto(
                user.Id,
                user.Email,
                user.FullName,
                user.Role.RoleName,
                user.Designation,
                user.BranchId.Value,
                user.Branch.BranchName,
                user.TenantId,
                user.Tenant.Name,
                user.AvatarUrl
            )
        );

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
        // ============================================================

        var user = await _context.Users
            .Include(u => u.Tenant)
            .Include(u => u.Branch)
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

        // Make sure the user's branch belongs to the same tenant.
        if (user.Branch.TenantId != user.TenantId)
        {
            return Unauthorized(new
            {
                message =
                    "Your account has an invalid branch configuration."
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

            avatarUrl = user.AvatarUrl
        };

        return Ok(response);
    }
}