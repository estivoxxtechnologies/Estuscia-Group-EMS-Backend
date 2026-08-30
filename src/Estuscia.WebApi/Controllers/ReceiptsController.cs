using Estuscia.Application.Common.DTOs;
using Estuscia.Application.Common.Interfaces;
using Estuscia.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Estuscia.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReceiptsController : ControllerBase
{
    private readonly IAppDbContext _context;
    private readonly ICurrentTenantService _tenantService;

    public ReceiptsController(IAppDbContext context, ICurrentTenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetReceipts([FromQuery] string? branch)
    {
        var query = _context.CustomerReceipts.Include(r => r.IssuedByStaff).AsNoTracking();

        if (!string.IsNullOrEmpty(branch) && branch != "All Branches")
        {
            query = query.Where(r => r.BranchName == branch);
        }

        var receipts = await query.OrderByDescending(r => r.CreatedAtUtc).ToListAsync();
        return Ok(receipts);
    }

    [HttpPost("issue")]
    public async Task<IActionResult> IssueReceipt([FromBody] IssueReceiptDto dto)
    {
        var currentUserId = _tenantService.UserId!.Value;
        var user = await _context.Users.FindAsync(currentUserId);
        if (user == null) return Unauthorized();

        var receiptNumber = $"RCP-{DateTime.UtcNow.Year}-{Random.Shared.Next(1000, 9999)}";

        var receipt = new CustomerReceipt
        {
            BranchName = user.BranchName,
            ReceiptNumber = receiptNumber,
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            CustomerEmail = dto.CustomerEmail,
            DepositAmount = dto.DepositAmount,
            SlabTierName = dto.SlabTierName,
            AnnualYieldPercent = dto.AnnualYieldPercent,
            LockinPeriodMonths = dto.LockinPeriodMonths,
            PaymentMode = dto.PaymentMode,
            BankReferenceNumber = dto.BankReferenceNumber,
            PayoutFrequency = dto.PayoutFrequency,
            IssuedByStaffId = currentUserId,
            Status = "Confirmed",
            DigitalSecurityHash = Guid.NewGuid().ToString("N").ToUpper()
        };

        _context.CustomerReceipts.Add(receipt);
        await _context.SaveChangesAsync();

        return Ok(receipt);
    }
}
