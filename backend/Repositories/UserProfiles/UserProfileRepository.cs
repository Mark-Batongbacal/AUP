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

    public Task<UserProfile?> GetByExternalAuthAsync(
        string provider,
        string externalAuthId,
        CancellationToken cancellationToken = default) =>
        _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                profile => profile.ExternalAuthProvider == provider &&
                    profile.ExternalAuthId == externalAuthId,
                cancellationToken);

    public Task<UserProfile?> GetActiveByExternalAuthAsync(
        string provider,
        string externalAuthId,
        CancellationToken cancellationToken = default) =>
        _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                profile => profile.ExternalAuthProvider == provider &&
                    profile.ExternalAuthId == externalAuthId &&
                    profile.IsActive,
                cancellationToken);

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

        existing.ExternalAuthProvider = profile.ExternalAuthProvider;
        existing.ExternalAuthId = profile.ExternalAuthId;
        existing.Email = profile.Email;
        existing.FirstName = profile.FirstName;
        existing.LastName = profile.LastName;
        existing.PhoneNumber = profile.PhoneNumber;
        existing.Role = profile.Role;
        existing.ProfileImageUrl = profile.ProfileImageUrl;
        existing.PreferredLanguage = profile.PreferredLanguage;
        existing.IsActive = profile.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<UserProfile?> UpdateEditableFieldsAsync(
        Guid userId,
        string? firstName,
        string? lastName,
        string? phoneNumber,
        string? profileImageUrl,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserProfiles.FirstOrDefaultAsync(
            currentProfile => currentProfile.UserId == userId && currentProfile.IsActive,
            cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.FirstName = firstName;
        existing.LastName = lastName;
        existing.PhoneNumber = phoneNumber;
        existing.ProfileImageUrl = profileImageUrl;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeactivateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserProfiles.FirstOrDefaultAsync(
            profile => profile.UserId == userId && profile.IsActive,
            cancellationToken);
        if (existing is null)
        {
            return false;
        }

        // UserProfiles.Email and the external-auth pair are unique. A plain IsActive=false soft
        // delete would permanently reserve those identities and prevent the person from creating
        // a fresh account later. Keep the row/UserId for historical foreign keys, but release all
        // login identities and remove profile PII.
        existing.Email = $"deleted+{userId:N}@deleted.tuki.invalid";
        existing.ExternalAuthProvider = null;
        existing.ExternalAuthId = null;
        existing.FirstName = null;
        existing.LastName = null;
        existing.PhoneNumber = null;
        existing.ProfileImageUrl = null;
        existing.IsEmailVerified = false;
        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;

        var localCredential = await _context.LocalUserCredentials
            .FirstOrDefaultAsync(credential => credential.UserId == userId, cancellationToken);
        if (localCredential is not null)
        {
            _context.LocalUserCredentials.Remove(localCredential);
        }

        var passwordTokens = await _context.PasswordResetTokens
            .Where(token => token.UserId == userId)
            .ToListAsync(cancellationToken);
        if (passwordTokens.Count > 0)
        {
            _context.PasswordResetTokens.RemoveRange(passwordTokens);
        }

        var verificationTokens = await _context.EmailVerificationTokens
            .Where(token => token.UserId == userId)
            .ToListAsync(cancellationToken);
        if (verificationTokens.Count > 0)
        {
            _context.EmailVerificationTokens.RemoveRange(verificationTokens);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(profile => profile.UserId == userId, cancellationToken);
}
