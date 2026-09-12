namespace CrownRank.Api.Infrastructure;

public sealed class ApiProblemException(int statusCode, string title, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}
