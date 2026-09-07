namespace CrownRank.Domain.Creators;

public sealed class Creator
{
    private Creator() { }

    public Creator(Guid id, string username, DateTimeOffset createdAt)
    {
        Id = id;
        Username = string.IsNullOrWhiteSpace(username)
            ? throw new ArgumentException("Username is required.", nameof(username))
            : username.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}

