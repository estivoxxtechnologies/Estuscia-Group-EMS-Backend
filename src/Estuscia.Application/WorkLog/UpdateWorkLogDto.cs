namespace Estuscia.Application.Common.DTOs;

public class UpdateWorkLogDto
{
    public DateOnly? WorkDate { get; set; }

    public string? Narration { get; set; }

    public int? CallsMade { get; set; }

    public int? CallsConnected { get; set; }

    public int? LeadsRespondedWell { get; set; }

    public int? FollowUpsScheduled { get; set; }
}