using CrownRank.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace CrownRank.Infrastructure.Images;

public sealed class LocalProfileImageService(IHostEnvironment environment, IOptions<ProfileImageOptions> options) : IProfileImageService
{
    private static readonly HashSet<string> AllowedTypes = ["image/jpeg", "image/png", "image/webp", "image/gif", "image/bmp"];
    private readonly ProfileImageOptions _options = options.Value;

    public async Task<StoredProfileImage> SaveAsync(ProfileImageUpload upload, CancellationToken cancellationToken)
    {
        if (upload.Length is <= 0 || upload.Length > _options.MaxBytes) throw new ArgumentException($"Image must be between 1 byte and {_options.MaxBytes / 1024 / 1024} MB.");
        if (!AllowedTypes.Contains(upload.ContentType.ToLowerInvariant())) throw new ArgumentException("Use JPEG, PNG, WebP, GIF, or BMP.");
        if (!upload.Content.CanSeek) throw new ArgumentException("The image stream must support seeking.");
        var position = upload.Content.Position;
        var decoder = new DecoderOptions { MaxFrames = 1 };
        try
        {
            var info = await Image.IdentifyAsync(decoder, upload.Content, cancellationToken);
            if ((long)info.Width * info.Height > _options.MaxPixels) throw new ArgumentException("Image pixel dimensions are too large.");
            if (info.Metadata.DecodedImageFormat is not { } format || !AllowedTypes.Contains(format.DefaultMimeType))
                throw new ArgumentException("Use JPEG, PNG, WebP, GIF, or BMP.");
        }
        catch (UnknownImageFormatException ex) { throw new ArgumentException("The uploaded file is not a supported image.", ex); }
        catch (InvalidImageContentException ex) { throw new ArgumentException("The uploaded image is damaged.", ex); }
        finally { upload.Content.Position = position; }
        using var image = await DecodeAsync(decoder, upload.Content, cancellationToken);
        if ((long)image.Width * image.Height > _options.MaxPixels) throw new ArgumentException("Image pixel dimensions are too large.");
        image.Mutate(context => context.AutoOrient().Resize(new ResizeOptions
        {
            Size = new Size(_options.OutputSize, _options.OutputSize), Mode = ResizeMode.Crop, Position = AnchorPositionMode.Center
        }));
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        var key = $"{Guid.NewGuid():N}.webp";
        var directory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, _options.Directory));
        Directory.CreateDirectory(directory);
        await image.SaveAsWebpAsync(Path.Combine(directory, key), new WebpEncoder { Quality = 82 }, cancellationToken);
        return new StoredProfileImage($"{_options.PublicPath.TrimEnd('/')}/{key}", key);
    }

    private static async Task<Image> DecodeAsync(DecoderOptions options, Stream content, CancellationToken cancellationToken)
    {
        try { return await Image.LoadAsync(options, content, cancellationToken); }
        catch (UnknownImageFormatException ex) { throw new ArgumentException("The uploaded file is not a supported image.", ex); }
        catch (InvalidImageContentException ex) { throw new ArgumentException("The uploaded image is damaged.", ex); }
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.Combine(environment.ContentRootPath, _options.Directory, Path.GetFileName(storageKey));
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}

