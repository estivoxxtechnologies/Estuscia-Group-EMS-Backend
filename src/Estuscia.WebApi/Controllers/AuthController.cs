using Estuscia.Application.Common.DTOs;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAppDbContext _context;
    private readonly IJwtTokenGenerator _jwtGenerator;

    // ============================================================
    // SYSTEM DEFAULT CURRENCY
    // ============================================================

    // Currency ID 2 = INR according to your Currency table.
    private const int SuperAdminCurrencyId = 2;

    // ============================================================
    // TENANT-LEVEL ROLES
    // ============================================================

    private static readonly string[] TenantLevelRoles =
    {
        "super_admin",
        "company_admin",
        "hr_ops"
    };

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
        // ========================================================
        // VALIDATE REQUEST
        // ========================================================

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

        // ========================================================
        // FIND USER
        //
        // Ignore tenant query filters because SuperAdmin can have
        // TenantId = null and normal authentication must still
        // locate the user.
        //
        // Load:
        // - Tenant
        // - Tenant.DefaultCurrency
        // - Branch
        // - Branch.Currency
        // - Role
        // ========================================================

        var user = await _context.Users
            .IgnoreQueryFilters()

            .Include(u => u.Tenant)
                .ThenInclude(t => t.DefaultCurrency)

            .Include(u => u.Branch)
                .ThenInclude(b => b.Currency)

            .Include(u => u.Role)

            .FirstOrDefaultAsync(
                u => u.Email.ToLower() == email);

        // ========================================================
        // INVALID USER
        // ========================================================

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        // ========================================================
        // ACCOUNT STATUS
        // ========================================================

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message =
                    "Your account is inactive. Please contact an administrator."
            });
        }

        // ========================================================
        // PASSWORD
        // ========================================================

        if (!BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        // ========================================================
        // ROLE VALIDATION
        // ========================================================

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

        var roleName = user.Role.RoleName
            .Trim()
            .ToLowerInvariant();

        // ========================================================
        // SUPER ADMIN
        //
        // SuperAdmin is system-level.
        //
        // TenantId = null
        // BranchId = null
        // Currency = Currency ID 2 (INR)
        // ========================================================

        if (roleName == "super_admin")
        {
            if (user.TenantId.HasValue)
            {
                return Unauthorized(new
                {
                    message =
                        "SuperAdmin must not be associated with a tenant."
                });
            }

            if (user.BranchId.HasValue)
            {
                return Unauthorized(new
                {
                    message =
                        "SuperAdmin must not be associated with a branch."
                });
            }
        }
        else
        {
            // ====================================================
            // NON-SUPERADMIN MUST BELONG TO A TENANT
            // ====================================================

            if (!user.TenantId.HasValue || user.Tenant == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Your account is not associated with an organization."
                });
            }

            // ====================================================
            // TENANT STATUS
            // ====================================================

            if (!user.Tenant.IsActive)
            {
                return Unauthorized(new
                {
                    message = "This organization is inactive."
                });
            }

            // ====================================================
            // TENANT DEFAULT CURRENCY
            //
            // Required because CompanyAdmin / HR Ops can operate
            // without a branch.
            // ====================================================

            if (user.Tenant.DefaultCurrency == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Your organization does not have a valid default currency configured."
                });
            }
        }

        // ========================================================
        // BRANCH VALIDATION
        //
        // SuperAdmin:
        //     BranchId must be null.
        //
        // CompanyAdmin / HR Ops:
        //     BranchId can be null.
        //
        // Other roles:
        //     BranchId is required.
        // ========================================================

        var requiresBranch =
            !TenantLevelRoles.Contains(roleName);

        if (roleName == "super_admin")
        {
            // Already validated above.
        }
        else if (requiresBranch)
        {
            // ====================================================
            // BRANCH IS REQUIRED
            // ====================================================

            if (!user.BranchId.HasValue ||
                user.Branch == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Your account is not associated with a valid branch."
                });
            }

            // ====================================================
            // BRANCH STATUS
            // ====================================================

            if (!user.Branch.IsActive)
            {
                return Unauthorized(new
                {
                    message =
                        "Your branch is inactive. Please contact an administrator."
                });
            }

            // ====================================================
            // BRANCH MUST BELONG TO SAME TENANT
            // ====================================================

            if (!user.TenantId.HasValue ||
                user.Branch.TenantId != user.TenantId.Value)
            {
                return Unauthorized(new
                {
                    message =
                        "Your account has an invalid branch configuration."
                });
            }

            // ====================================================
            // BRANCH CURRENCY
            // ====================================================

            if (user.Branch.Currency == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Your branch does not have a valid currency configured."
                });
            }
        }
        else
        {
            // ====================================================
            // COMPANY ADMIN / HR OPS
            //
            // Branch is OPTIONAL.
            //
            // If branch exists:
            //     validate branch and use branch currency.
            //
            // If branch does not exist:
            //     use tenant default currency.
            // ====================================================

            if (user.BranchId.HasValue)
            {
                if (user.Branch == null)
                {
                    return Unauthorized(new
                    {
                        message =
                            "Your account has an invalid branch configuration."
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

                if (!user.TenantId.HasValue ||
                    user.Branch.TenantId != user.TenantId.Value)
                {
                    return Unauthorized(new
                    {
                        message =
                            "Your account has an invalid branch configuration."
                    });
                }

                if (user.Branch.Currency == null)
                {
                    return Unauthorized(new
                    {
                        message =
                            "Your branch does not have a valid currency configured."
                    });
                }
            }
        }

        // ========================================================
        // RESOLVE EFFECTIVE CURRENCY
        // ========================================================

        Currency? effectiveCurrency;

        if (roleName == "super_admin")
        {
            // ====================================================
            // SUPERADMIN
            //
            // Always INR / Currency ID 2.
            //
            // We intentionally do not use Tenant or Branch.
            // ====================================================

            effectiveCurrency =
                await _context.Currencies
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(
                        c => c.Id == SuperAdminCurrencyId);

            if (effectiveCurrency == null)
            {
                return Unauthorized(new
                {
                    message =
                        "System default currency (INR) is not configured."
                });
            }
        }
        else if (user.Branch?.Currency != null)
        {
            // ====================================================
            // BRANCH CURRENCY
            //
            // Applies to:
            // - Branch-level users
            // - CompanyAdmin with a branch
            // - HR Ops with a branch
            // ====================================================

            effectiveCurrency = user.Branch.Currency;
        }
        else
        {
            // ====================================================
            // TENANT DEFAULT CURRENCY
            //
            // Applies to:
            // - CompanyAdmin without branch
            // - HR Ops without branch
            // ====================================================

            effectiveCurrency = user.Tenant!.DefaultCurrency;

            if (effectiveCurrency == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Your organization does not have a valid default currency configured."
                });
            }
        }

        // ========================================================
        // GENERATE TOKENS
        // ========================================================

        var accessToken =
            _jwtGenerator.GenerateToken(user);

        var refreshToken =
            _jwtGenerator.GenerateRefreshToken();

        // ========================================================
        // RESPONSE
        // ========================================================

        var response = new
        {
            accessToken,
            refreshToken,

            user = BuildUserResponse(
                user,
                effectiveCurrency)
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
        // ========================================================
        // GET USER ID FROM JWT
        // ========================================================

        var userIdClaim =
            User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(
                System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("user_id")?.Value;

        // ========================================================
        // CURRENT IDS ARE INT
        // ========================================================

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        // ========================================================
        // LOAD USER
        // ========================================================

        var user = await _context.Users
            .IgnoreQueryFilters()

            .Include(u => u.Tenant)
                .ThenInclude(t => t.DefaultCurrency)

            .Include(u => u.Branch)
                .ThenInclude(b => b.Currency)

            .Include(u => u.Role)

            .FirstOrDefaultAsync(
                u => u.Id == userId);

        // ========================================================
        // USER NOT FOUND
        // ========================================================

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "User account could not be found."
            });
        }

        // ========================================================
        // ACCOUNT STATUS
        // ========================================================

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "Your account is inactive."
            });
        }

        // ========================================================
        // ROLE
        // ========================================================

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
                    "Your account does not have a valid active role."
            });
        }

        var roleName = user.Role.RoleName
            .Trim()
            .ToLowerInvariant();

        // ========================================================
        // SUPER ADMIN
        // ========================================================

        if (roleName == "super_admin")
        {
            // ====================================================
            // SUPERADMIN MUST NOT HAVE TENANT OR BRANCH
            // ====================================================

            if (user.TenantId.HasValue)
            {
                return Unauthorized(new
                {
                    message =
                        "SuperAdmin must not be associated with a tenant."
                });
            }

            if (user.BranchId.HasValue)
            {
                return Unauthorized(new
                {
                    message =
                        "SuperAdmin must not be associated with a branch."
                });
            }
        }
        else
        {
            // ====================================================
            // NON-SUPERADMIN TENANT
            // ====================================================

            if (!user.TenantId.HasValue ||
                user.Tenant == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Your account is not associated with an organization."
                });
            }

            // ====================================================
            // TENANT STATUS
            // ====================================================

            if (!user.Tenant.IsActive)
            {
                return Unauthorized(new
                {
                    message = "This organization is inactive."
                });
            }

            // ====================================================
            // TENANT DEFAULT CURRENCY
            // ====================================================

            if (user.Tenant.DefaultCurrency == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Your organization does not have a valid default currency configured."
                });
            }
        }

        // ========================================================
        // BRANCH VALIDATION
        // ========================================================

        var requiresBranch =
            !TenantLevelRoles.Contains(roleName);

        if (roleName == "super_admin")
        {
            // No branch required.
        }
        else if (requiresBranch)
        {
            // ====================================================
            // BRANCH REQUIRED
            // ====================================================

            if (!user.BranchId.HasValue ||
                user.Branch == null)
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
                        "Your branch is inactive."
                });
            }

            if (!user.TenantId.HasValue ||
                user.Branch.TenantId != user.TenantId.Value)
            {
                return Unauthorized(new
                {
                    message =
                        "Your account has an invalid branch configuration."
                });
            }

            if (user.Branch.Currency == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Your branch does not have a valid currency configured."
                });
            }
        }
        else
        {
            // ====================================================
            // COMPANY ADMIN / HR OPS
            //
            // Branch is optional.
            // ====================================================

            if (user.BranchId.HasValue)
            {
                if (user.Branch == null)
                {
                    return Unauthorized(new
                    {
                        message =
                            "Your account has an invalid branch configuration."
                    });
                }

                if (!user.Branch.IsActive)
                {
                    return Unauthorized(new
                    {
                        message =
                            "Your branch is inactive."
                    });
                }

                if (!user.TenantId.HasValue ||
                    user.Branch.TenantId != user.TenantId.Value)
                {
                    return Unauthorized(new
                    {
                        message =
                            "Your account has an invalid branch configuration."
                    });
                }

                if (user.Branch.Currency == null)
                {
                    return Unauthorized(new
                    {
                        message =
                            "Your branch does not have a valid currency configured."
                    });
                }
            }
        }

        // ========================================================
        // RESOLVE EFFECTIVE CURRENCY
        // ========================================================

        Currency? effectiveCurrency;

        if (roleName == "super_admin")
        {
            // ====================================================
            // SUPERADMIN ALWAYS USES INR / CURRENCY ID 2
            // ====================================================

            effectiveCurrency =
                await _context.Currencies
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(
                        c => c.Id == SuperAdminCurrencyId);

            if (effectiveCurrency == null)
            {
                return Unauthorized(new
                {
                    message =
                        "System default currency (INR) is not configured."
                });
            }
        }
        else if (user.Branch?.Currency != null)
        {
            // ====================================================
            // BRANCH CURRENCY
            // ====================================================

            effectiveCurrency = user.Branch.Currency;
        }
        else
        {
            // ====================================================
            // TENANT DEFAULT CURRENCY
            // ====================================================

            effectiveCurrency = user.Tenant!.DefaultCurrency;

            if (effectiveCurrency == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Your organization does not have a valid default currency configured."
                });
            }
        }

        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(
            BuildUserResponse(
                user,
                effectiveCurrency));
    }

    // ============================================================
    // BUILD USER RESPONSE
    // ============================================================

    private static object BuildUserResponse(
        ApplicationUser user,
        Currency effectiveCurrency)
    {
        return new
        {
            userId = user.Id,

            username = user.FullName,

            email = user.Email,

            roleId = user.RoleNumber,

            roleName = user.Role!.RoleName,

            designation = user.Designation,

            tenantId = user.TenantId,

            tenantName = user.Tenant?.Name,

            branchId = user.BranchId,

            branchName = user.Branch?.BranchName,

            avatarUrl = user.AvatarUrl,

            // ====================================================
            // EFFECTIVE CURRENCY
            //
            // This is the currency the frontend should use for
            // financial calculations and display.
            // ====================================================

            currency = new
            {
                id = effectiveCurrency.Id,

                code = effectiveCurrency.Code,

                name = effectiveCurrency.Name,

                symbol = effectiveCurrency.Symbol
            }
        };
    }
}
