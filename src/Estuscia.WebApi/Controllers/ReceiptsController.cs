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

    public ReceiptsController(
        IAppDbContext context,
        ICurrentTenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    // ============================================================
    // GET RECEIPTS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetReceipts(
        [FromQuery] Guid? branchId)
    {
        var query = _context.CustomerReceipts
            .Include(r => r.IssuedByStaff)
            .Include(r => r.Branch)
            .AsNoTracking();

        if (branchId.HasValue)
        {
            query = query.Where(r =>
                r.BranchId == branchId.Value);
        }

        var receipts = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        return Ok(receipts);
    }

    // ============================================================
    // ISSUE RECEIPT
    // ============================================================

    [HttpPost("issue")]
    public async Task<IActionResult> IssueReceipt(
        [FromBody] IssueReceiptDto dto)
    {
        if (!_tenantService.UserId.HasValue)
            return Unauthorized();

        var currentUserId = _tenantService.UserId.Value;

        var user = await _context.Users
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (user == null)
            return Unauthorized();

        if (user.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Your account is not assigned to a branch."
            });
        }

        var receiptNumber =
            $"RCP-{DateTime.UtcNow.Year}-{Random.Shared.Next(1000, 9999)}";

        var receipt = new CustomerReceipt
        {
            TenantId = user.TenantId,
            BranchId = user.BranchId,

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

            DigitalSecurityHash =
                Guid.NewGuid().ToString("N").ToUpper()
        };

        _context.CustomerReceipts.Add(receipt);

        await _context.SaveChangesAsync();

        return Ok(receipt);
    }
}