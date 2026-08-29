using Estuscia.Application.Common.DTOs;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAppDbContext _context;
    private readonly IJwtTokenGenerator _jwtGenerator;

    public AuthController(IAppDbContext context, IJwtTokenGenerator jwtGenerator)
    {
        _context = context;
        _jwtGenerator = jwtGenerator;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or credentials" });
        }

        var token = _jwtGenerator.GenerateToken(user);
        var refreshToken = _jwtGenerator.GenerateRefreshToken();

        return Ok(new AuthResponseDto(
            token,
            refreshToken,
            new UserDto(
                user.Id,
                user.Email,
                user.FullName,
                user.Role.ToString().ToLower(),
                user.Designation,
                user.BranchName,
                user.TenantId,
                user.Tenant.Name,
                user.AvatarUrl
            )
        ));
    }
}
