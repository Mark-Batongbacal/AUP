using System.Security.Cryptography;
using System.Text;
using backend.Models.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services.Email;

public interface IEmailVerificationService
{
    Task SendVerificationEmailAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ConfirmAsync(string code, CancellationToken cancellationToken = default);
}

public sealed class EmailVerificationService(
    TukiDbContext context,
    IEmailSender emailSender,
    IOptions<EmailOptions> options) : IEmailVerificationService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(30);
    private readonly EmailOptions _options = options.Value;

    public async Task SendVerificationEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await context.UserProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
        if (user is null || user.IsEmailVerified)
        {
            return;
        }

        var code = VerificationCode.Generate();
        context.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = userId,
            TokenHash = VerificationCode.Hash(code),
            ExpiresAt = DateTime.UtcNow.Add(CodeLifetime),
        });
        await context.SaveChangesAsync(cancellationToken);

        var subject = $"Verify your {_options.AppDisplayName} email";
        var html =
            $"<p>Your {_options.AppDisplayName} verification code is:</p>" +
            $"<p style=\"font-size:24px;font-weight:bold;letter-spacing:4px;\">{code}</p>" +
            $"<p>This code expires in {CodeLifetime.TotalMinutes:0} minutes.</p>";

        await emailSender.SendAsync(user.Email, subject, html, $"Your verification code is {code}.", cancellationToken);
    }

    public async Task<bool> ConfirmAsync(string code, CancellationToken cancellationToken = default)
    {
        var hash = VerificationCode.Hash(code);
        var now = DateTime.UtcNow;
        var token = await context.EmailVerificationTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(
                t => t.TokenHash == hash && t.ConsumedAt == null && t.ExpiresAt > now,
                cancellationToken);
        if (token is null)
        {
            return false;
        }

        token.ConsumedAt = now;
        token.User.IsEmailVerified = true;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

internal static class VerificationCode
{
    public const int Length = 8;

    public static string Generate() =>
        RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8");

    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));
}
