using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for application user profiles only. Authentication and password handling stay
/// outside this repository.
/// </summary>
public sealed class UserProfileRepository(SupabaseDbContext context) : IUserProfileRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<user_profile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.user_profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.user_id == userId, cancellationToken);

    public Task<user_profile?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.user_profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.user_id == userId && profile.is_active, cancellationToken);

    public async Task<user_profile> AddOrUpdateAsync(user_profile profile, CancellationToken cancellationToken = default)
    {
        var existing = await _context.user_profiles.FirstOrDefaultAsync(
            currentProfile => currentProfile.user_id == profile.user_id,
            cancellationToken);

        if (existing is null)
        {
            await _context.user_profiles.AddAsync(profile, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return profile;
        }

        existing.first_name = profile.first_name;
        existing.last_name = profile.last_name;
        existing.phone_number = profile.phone_number;
        existing.role = profile.role;
        existing.profile_image_url = profile.profile_image_url;
        existing.is_active = profile.is_active;
        existing.updated_at = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.user_profiles
            .AsNoTracking()
            .AnyAsync(profile => profile.user_id == userId, cancellationToken);
}
