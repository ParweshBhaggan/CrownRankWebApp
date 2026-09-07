namespace CrownRank.Infrastructure.Images;

public sealed class ProfileImageOptions
{
    public const string SectionName = "ProfileImages";
    public string Directory { get; init; } = "wwwroot/uploads/profiles";
    public string PublicPath { get; init; } = "/uploads/profiles";
    public long MaxBytes { get; init; } = 8 * 1024 * 1024;
    public int MaxPixels { get; init; } = 25_000_000;
    public int OutputSize { get; init; } = 1024;
}
