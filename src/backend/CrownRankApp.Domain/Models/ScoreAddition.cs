namespace CrownRankApp.Domain.Models;

// One row per opening score or boost, so daily totals can be calculated.
public class ScoreAddition : Entity
{
    public ScoreAddition() { }
    public ScoreAddition(Guid referenceId) : base(referenceId) { }
    public Guid EntryId { get; set; }
    public Entry Entry { get; set; } = null!;
    public decimal Amount { get; set; }
}
