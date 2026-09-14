using CrownRank.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace CrownRank.Infrastructure.Storage;

public sealed class LocalProfileImageStorage(string directory) : IProfileImageStorage
{
    private const int MaxBytes = 5 * 1024 * 1024;

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
        if (image.Width > 10000 || image.Height > 10000) throw new InvalidDataException("Image dimensions exceed the limit.");
        image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(300, 250)
        }));
        Directory.CreateDirectory(directory);
        var key = $"{Guid.NewGuid():N}.png";
        var destination = Path.Combine(directory, key);
        try { await image.SaveAsPngAsync(destination, ct); }
        catch { if (File.Exists(destination)) File.Delete(destination); throw; }
        return key;
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        if (key != Path.GetFileName(key) || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _) ||
            Path.GetExtension(key) != ".png") throw new ArgumentException("Invalid image key.", nameof(key));
        File.Delete(Path.Combine(directory, key));
        return Task.CompletedTask;
    }
}
