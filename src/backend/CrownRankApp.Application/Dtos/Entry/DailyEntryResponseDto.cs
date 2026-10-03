namespace CrownRankApp.Application.Dtos.Entry;

public class DailyEntryResponseDto
{
    public EntryResponseDto Entry { get; set; } = new();
    public decimal DailyScore { get; set; }
    public DateTime ScoreReachedDate { get; set; }
}
