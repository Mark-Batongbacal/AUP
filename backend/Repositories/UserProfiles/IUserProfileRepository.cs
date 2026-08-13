using backend.Models.Database;

namespace backend.Repositories;

public interface IUserProfileRepository
{
    Task<user_profile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<user_profile?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<user_profile> AddOrUpdateAsync(user_profile profile, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);
}
