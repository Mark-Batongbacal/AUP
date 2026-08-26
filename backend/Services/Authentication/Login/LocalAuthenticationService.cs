using System.Security.Cryptography;
using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Authentication.Login;

public interface ILocalAuthenticationService
{
    bool CredentialsAreValid(string userName, string password);

    Task<DateTime?> GetCredentialUpdatedAtAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task StoreCredentialAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken = default);
}

public sealed class LocalAuthenticationService(TukiDbContext context) : ILocalAuthenticationService
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Algorithm = "PBKDF2-SHA256";

    private readonly TukiDbContext _context = context;

    public bool CredentialsAreValid(string userName, string password)
    {
        var normalizedUserName = userName.Trim();
        var credential = _context.LocalUserCredentials
            .AsNoTracking()
            .Include(current => current.User)
            .FirstOrDefault(current =>
                current.User.Email == normalizedUserName &&
                current.User.IsActive);

        return credential is not null && VerifyPassword(password, credential.PasswordHash);
    }

    public Task<DateTime?> GetCredentialUpdatedAtAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _context.LocalUserCredentials
            .AsNoTracking()
            .Where(current =>
                current.UserId == userId &&
                current.UpdatedAt > current.CreatedAt)
            .Select(current => (DateTime?)current.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task StoreCredentialAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var existing = await _context.LocalUserCredentials
            .FirstOrDefaultAsync(current => current.UserId == userId, cancellationToken);

        if (existing is null)
        {
            await _context.LocalUserCredentials.AddAsync(new LocalUserCredential
            {
                UserId = userId,
                PasswordHash = HashPassword(password),
                CreatedAt = now,
                UpdatedAt = now,
            }, cancellationToken);
        }
        else
        {
            existing.PasswordHash = HashPassword(password);
            existing.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join(
            '$',
            Algorithm,
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    private static bool VerifyPassword(string password, string encodedHash)
    {
        var parts = encodedHash.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal))
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations < 100_000)
            return false;

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
