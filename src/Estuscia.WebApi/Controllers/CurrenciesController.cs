using Estuscia.Application.Common.DTOs.Currency;
using Estuscia.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CurrenciesController : ControllerBase
{
    private readonly IAppDbContext _context;

    public CurrenciesController(IAppDbContext context)
    {
        _context = context;
    }

    [HttpGet("combo")]
    public async Task<ActionResult<IEnumerable<CurrencyDto>>> GetCombo(
        CancellationToken cancellationToken)
    {
        var currencies = await _context.Currencies
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new CurrencyDto(
                x.Id,
                x.Code,
                x.Name,
                x.Symbol,
                x.IsActive
            ))
            .ToListAsync(cancellationToken);

        return Ok(currencies);
    }
}