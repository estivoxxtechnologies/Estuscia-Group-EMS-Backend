using Estuscia.Domain.Enums;

namespace Estuscia.Application.Common.DTOs;

public class AssignSalesLeadsRequestDto
{
    public int BranchId { get; set; }

    public int AssignedToUserId { get; set; }

    public List<SalesLeadNumberDto> Leads { get; set; }
        = new();
}

public class SalesLeadNumberDto
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string? CustomerName { get; set; }
}


public class UpdateSalesLeadOutcomeRequestDto
{
    public SalesLeadOutcome Outcome { get; set; }

    public string? Notes { get; set; }
}


public class SalesLeadResponseDto
{
    public int AssignmentId { get; set; }

    public int SalesLeadId { get; set; }

    public int TenantId { get; set; }

    public int BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public int AssignedToUserId { get; set; }

    public string AssignedToUserName { get; set; } = string.Empty;

    public string? AssignedToEmployeeCode { get; set; }

    public int AssignedByUserId { get; set; }

    public string AssignedByUserName { get; set; } = string.Empty;

    public DateTime AssignedAtUtc { get; set; }

    public SalesLeadOutcome Outcome { get; set; }

    public string? Notes { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}


public class SalesLeadMetricsDto
{
    public int CallsMade { get; set; }

    public int Connected { get; set; }

    public int RespondedWell { get; set; }

    public int FollowUp { get; set; }

    public int NotConnected { get; set; }

    public int OtherConnected { get; set; }

    public int Pending { get; set; }
}