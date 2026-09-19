using Estuscia.Domain.Enums;

namespace Estuscia.Application;

// ============================================================
// CREATE / ASSIGN
// ============================================================

public class CreateDeveloperWorkRequestDto
{
    public int BranchId { get; set; }

    public int AssignedToUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DeveloperWorkType WorkType { get; set; } =
        DeveloperWorkType.Other;

    public DeveloperWorkPriority Priority { get; set; } =
        DeveloperWorkPriority.Medium;

    public DateTime? DueDateUtc { get; set; }
}


// ============================================================
// UPDATE STATUS
// ============================================================

public class UpdateDeveloperWorkStatusRequestDto
{
    public DeveloperWorkStatus Status { get; set; }

    public string? CompletionNotes { get; set; }
}


// ============================================================
// UPDATE WORK DETAILS
// ============================================================

public class UpdateDeveloperWorkRequestDto
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DeveloperWorkType WorkType { get; set; }

    public DeveloperWorkPriority Priority { get; set; }

    public DateTime? DueDateUtc { get; set; }
}


// ============================================================
// RESPONSE
// ============================================================

public class DeveloperWorkResponseDto
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    // --------------------------------------------------------
    // Work
    // --------------------------------------------------------

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DeveloperWorkType WorkType { get; set; }

    public DeveloperWorkPriority Priority { get; set; }

    public DeveloperWorkStatus Status { get; set; }

    // --------------------------------------------------------
    // Assignment
    // --------------------------------------------------------

    public int AssignedToUserId { get; set; }

    public string AssignedToUserName { get; set; } = string.Empty;

    public string? AssignedToEmployeeCode { get; set; }

    public int AssignedByUserId { get; set; }

    public string AssignedByUserName { get; set; } = string.Empty;

    // --------------------------------------------------------
    // Dates
    // --------------------------------------------------------

    public DateTime AssignedAtUtc { get; set; }

    public DateTime? DueDateUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    // --------------------------------------------------------
    // Completion
    // --------------------------------------------------------

    public string? CompletionNotes { get; set; }
}