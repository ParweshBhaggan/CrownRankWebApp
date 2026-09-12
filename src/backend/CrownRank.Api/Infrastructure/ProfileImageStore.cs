namespace CrownRank.Api.Infrastructure;

public sealed record StoredImage(string Url, string StorageKey);

public interface IProfileImageStore
{
    Task<StoredImage> SaveAsync(IFormFile image, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed class LocalProfileImageStore(IWebHostEnvironment environment) : IProfileImageStore
{
    private const long MaximumBytes = 8 * 1024 * 1024;

    public async Task<StoredImage> SaveAsync(IFormFile image, CancellationToken cancellationToken)
    {
        if (image.Length is <= 0 or > MaximumBytes)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, "Invalid image",
                "Profile images must be between 1 byte and 8 MB.");

        await using var source = image.OpenReadStream();
        var header = new byte[12];
        var read = await source.ReadAsync(header, cancellationToken);
        source.Position = 0;
        var extension = DetectExtension(header.AsSpan(0, read));
        if (extension is null)
            throw new ApiProblemException(StatusCodes.Status400BadRequest, "Invalid image",
                "Profile images must be JPEG, PNG, WebP, GIF, or BMP files.");

        var storageKey = $"{Guid.NewGuid():N}.{extension}";
        var directory = Path.Combine(environment.WebRootPath, "uploads", "profiles");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, storageKey);
        await using var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous);
        await source.CopyToAsync(destination, cancellationToken);
        return new StoredImage($"/uploads/profiles/{storageKey}", storageKey);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileName = Path.GetFileName(storageKey);
        var path = Path.Combine(environment.WebRootPath, "uploads", "profiles", fileName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private static string? DetectExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "jpg";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return "webp";
        if (bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8))) return "gif";
        if (bytes.Length >= 2 && bytes[..2].SequenceEqual("BM"u8)) return "bmp";
        return null;
    }
}
