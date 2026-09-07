namespace CrownRank.Application.Abstractions;

public sealed record ProfileImageUpload(Stream Content, string FileName, string ContentType, long Length);
public sealed record StoredProfileImage(string Url, string StorageKey);

public interface IProfileImageService
{
    Task<StoredProfileImage> SaveAsync(ProfileImageUpload image, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
