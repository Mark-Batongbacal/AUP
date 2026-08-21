using backend.Models.Database;

namespace backend.Repositories;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserProfile?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserProfile?> GetByExternalAuthAsync(
        string provider,
        string externalAuthId,
        CancellationToken cancellationToken = default);

    Task<UserProfile?> GetActiveByExternalAuthAsync(
        string provider,
        string externalAuthId,
        CancellationToken cancellationToken = default);

    Task<UserProfile?> GetActiveByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserProfile?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserProfile> AddOrUpdateAsync(UserProfile profile, CancellationToken cancellationToken = default);

    Task<UserProfile?> UpdateEditableFieldsAsync(
        Guid userId,
        string? firstName,
        string? lastName,
        string? phoneNumber,
        string? profileImageUrl,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);
}
