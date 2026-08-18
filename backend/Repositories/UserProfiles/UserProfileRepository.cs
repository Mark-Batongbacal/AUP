using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for application User profiles only. Authentication and password handling stay
/// outside this repository.
/// </summary>
public sealed class UserProfileRepository(TukiDbContext context) : IUserProfileRepository
{
    private readonly TukiDbContext _context = context;

    public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

    public Task<UserProfile?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == userId && profile.IsActive, cancellationToken);

    public Task<UserProfile?> GetActiveByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(
            profile => profile.Email == email && profile.IsActive, cancellationToken);

    public Task<UserProfile?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(
            profile => profile.Email == email, cancellationToken);

    public async Task<UserProfile> AddOrUpdateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserProfiles.FirstOrDefaultAsync(
            currentProfile => currentProfile.UserId == profile.UserId,
            cancellationToken);

        if (existing is null)
        {
            await _context.UserProfiles.AddAsync(profile, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return profile;
        }

        existing.FirstName = profile.FirstName;
        existing.LastName = profile.LastName;
        existing.PhoneNumber = profile.PhoneNumber;
        existing.Role = profile.Role;
        existing.ProfileImageUrl = profile.ProfileImageUrl;
        existing.IsActive = profile.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(profile => profile.UserId == userId, cancellationToken);
}
