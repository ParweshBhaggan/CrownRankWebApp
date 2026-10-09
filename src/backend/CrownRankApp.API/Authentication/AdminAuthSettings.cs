namespace CrownRankApp.API.Authentication;

public sealed class AdminAuthSettings
{
    public const string SectionName = "AdminAuth";

    public string Username
    {
        get;
        set;
    } = string.Empty;

    public string Password
    {
        get;
        set;
    } = string.Empty;

    public string SigningKey
    {
        get;
        set;
    } = string.Empty;

    public string Issuer
    {
        get;
        set;
    } = "CrownRankApp";

    public string Audience
    {
        get;
        set;
    } = "CrownRankAdmin";

    public int TokenLifetimeMinutes
    {
        get;
        set;
    } = 60;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("AdminAuth username and password are required.");
        }

        if (string.IsNullOrWhiteSpace(SigningKey) || SigningKey.Length < 32)
        {
            throw new InvalidOperationException("AdminAuth signing key must contain at least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("AdminAuth issuer and audience are required.");
        }

        if (TokenLifetimeMinutes is < 5 or > 1440)
        {
            throw new InvalidOperationException("AdminAuth token lifetime must be between 5 and 1440 minutes.");
        }
    }
}

public static class AdminRoles
{
    public const string Admin = "Admin";
}
