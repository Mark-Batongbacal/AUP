namespace backend.Services;

public sealed record StoredTricycleProof(string FileName, string ContentType);

public sealed record TricycleProofContent(Stream Content, string ContentType);

public interface ITricycleProofStorage
{
    Task<StoredTricycleProof> SaveAsync(
        Guid userId,
        ReadOnlyMemory<byte> content,
        string extension,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsOwnedAsync(
        Guid userId,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<TricycleProofContent?> OpenReadAsync(
        string fileName,
        CancellationToken cancellationToken = default);
}
