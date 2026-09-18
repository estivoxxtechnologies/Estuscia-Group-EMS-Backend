public class SubmitWorkLogDto
{
    public DateOnly? WorkDate { get; set; }

    public int WorkType { get; set; }

    public string Narration { get; set; } = string.Empty;

    public int? CallsMade { get; set; }

    public int? CallsConnected { get; set; }

    public int? LeadsRespondedWell { get; set; }

    public int? FollowUpsScheduled { get; set; }

    // These remain available for the future developer flow.
    public decimal? HoursSpent { get; set; }

    public string? FeaturesShipped { get; set; }

    public string? RepositoryPrLinks { get; set; }

    public string? BlockersEncountered { get; set; }
}