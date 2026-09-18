using CrownRank.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace CrownRank.Infrastructure.Storage;

public sealed class LocalProfileImageStorage(string directory, string publicBaseUrl = "/uploads/profiles")
    : IProfileImageStorage
{
    private const int MaxBytes = 5 * 1024 * 1024;
    private readonly string _publicBaseUrl =
        string.IsNullOrWhiteSpace(publicBaseUrl)
            ? throw new ArgumentException("A public image base URL is required.", nameof(publicBaseUrl))
            : publicBaseUrl.TrimEnd('/');

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (Path.GetExtension(fileName).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new ArgumentException("Only PNG, JPEG or WebP images are accepted.", nameof(fileName));
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > MaxBytes) throw new InvalidDataException("Image exceeds the 5 MB limit.");
            buffer.Write(chunk, 0, count);
        }
        buffer.Position = 0;
        using var image = await Image.LoadAsync(buffer, ct);
        if (image.Width > 10000 || image.Height > 10000)
            throw new InvalidDataException("Image dimensions exceed the limit.");
        image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(300, 250)
        }));
        Directory.CreateDirectory(directory);
        var fileKey = $"{Guid.NewGuid():N}.png";
        var destination = Path.Combine(directory, fileKey);
        try { await image.SaveAsPngAsync(destination, ct); }
        catch { if (File.Exists(destination)) File.Delete(destination); throw; }
        return $"{_publicBaseUrl}/{fileKey}";
    }

    public Task DeleteAsync(string imageUrl, CancellationToken ct = default)
    {
        var fileKey = FileKey(imageUrl);
        File.Delete(Path.Combine(directory, fileKey));
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string imageUrl, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = Path.Combine(directory, FileKey(imageUrl));
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous)
            : null;
        return Task.FromResult(stream);
    }

    private static string FileKey(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) throw new ArgumentException("Invalid image URL.", nameof(imageUrl));
        var path = Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute)
            ? absolute.AbsolutePath
            : imageUrl;
        var fileKey = Path.GetFileName(path);
        if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(fileKey), "N", out _) ||
            !string.Equals(Path.GetExtension(fileKey), ".png", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid image URL.", nameof(imageUrl));
        return fileKey;
    }
}
