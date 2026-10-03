namespace CrownRankApp.Application.Dtos.Entry;

public class BoostScoreDto
{
    public decimal Amount
    {
        get;
        set;
    }
    // Optional for direct API calls. The frontend reuses this ID on retries.
    public Guid? ReferenceId
    {
        get;
        set;
    }
}
