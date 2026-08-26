namespace backend.Services;

public sealed class FileSystemTricycleProofStorage(
    IWebHostEnvironment hostingEnvironment,
    IConfiguration configuration) : ITricycleProofStorage
{
    private readonly string _storageRoot = ResolveStorageRoot(hostingEnvironment, configuration);

    public async Task<StoredTricycleProof> SaveAsync(
        Guid userId,
        ReadOnlyMemory<byte> content,
        string extension,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storageRoot);

        var safeExtension = extension.Trim().TrimStart('.').ToLowerInvariant();
        var fileName = $"{userId:N}-{Guid.NewGuid():N}.{safeExtension}";
        var filePath = Path.Combine(_storageRoot, fileName);

        await File.WriteAllBytesAsync(filePath, content.ToArray(), cancellationToken);
        return new StoredTricycleProof(fileName, contentType);
    }

    public Task<bool> ExistsOwnedAsync(
        Guid userId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSafeFileName(fileName) ||
            !fileName.StartsWith($"{userId:N}-", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(File.Exists(Path.Combine(_storageRoot, fileName)));
    }

    public Task<TricycleProofContent?> OpenReadAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSafeFileName(fileName))
        {
            return Task.FromResult<TricycleProofContent?>(null);
        }

        var contentType = ContentTypeFromExtension(Path.GetExtension(fileName));
        if (contentType is null)
        {
            return Task.FromResult<TricycleProofContent?>(null);
        }

        var filePath = Path.Combine(_storageRoot, fileName);
        if (!File.Exists(filePath))
        {
            return Task.FromResult<TricycleProofContent?>(null);
        }

        Stream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult<TricycleProofContent?>(new TricycleProofContent(stream, contentType));
    }

    private static bool IsSafeFileName(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal) &&
        fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string? ContentTypeFromExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };

    private static string ResolveStorageRoot(
        IWebHostEnvironment hostingEnvironment,
        IConfiguration configuration)
    {
        var configured = configuration["TricycleProofs:StoragePath"]?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Path.Combine(hostingEnvironment.ContentRootPath, "tricycle-submission-proofs");
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(hostingEnvironment.ContentRootPath, configured);
    }
}
