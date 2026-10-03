using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace CrownRankApp.Infrastructure.Payments;

public sealed class ProfileImageStorage(string root)
{
    public async Task<string> SaveAsync(Guid id, string dataUrl, CancellationToken ct)
    {
        if (dataUrl is null || dataUrl.Length > 7_000_000) throw new ArgumentException("Choose an image up to 5 MB.");
        var separator = dataUrl.IndexOf(',');
        if (separator < 0 || dataUrl[..separator] is not ("data:image/jpeg;base64" or "data:image/png;base64" or "data:image/webp;base64"))
            throw new ArgumentException("Choose a JPG, PNG, or WebP image.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[(separator + 1)..]); }
        catch (FormatException) { throw new ArgumentException("The image could not be decoded."); }
        if (bytes.Length > 5 * 1024 * 1024) throw new ArgumentException("Choose an image up to 5 MB.");
        try
        {
            using var stream = new MemoryStream(bytes);
            var info = await Image.IdentifyAsync(stream, ct);
            if (info.Width > 10000 || info.Height > 10000 || (long)info.Width * info.Height > 25_000_000)
                throw new ArgumentException("The image dimensions are too large.");
            stream.Position = 0;
            using var image = await Image.LoadAsync(stream, ct);
            image.Mutate(action => action.AutoOrient().Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max, Size = new Size(300, 250)
            }));
            while (image.Frames.Count > 1) image.Frames.RemoveFrame(1);
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = null;
            var folder = Path.Combine(root, "assets", "profiles");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{id:N}.webp");
            // Each submission owns its image; retries never replace another submission's file.
            if (!File.Exists(path))
            {
                var temporary = Path.Combine(folder, $"{id:N}-{Guid.NewGuid():N}.tmp");
                try
                {
                    await image.SaveAsync(temporary, new WebpEncoder { Quality = 85 }, ct);
                    try { File.Move(temporary, path); }
                    catch (IOException) when (File.Exists(path)) { }
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            return $"/assets/profiles/{id:N}.webp";
        }
        catch (UnknownImageFormatException) { throw new ArgumentException("The uploaded file is not a supported image."); }
        catch (InvalidImageContentException) { throw new ArgumentException("The uploaded image is invalid."); }
    }

    public void Delete(Guid id)
    {
        var path = Path.Combine(root, "assets", "profiles", $"{id:N}.webp");
        if (File.Exists(path)) File.Delete(path);
    }
}
