using backend.Models.Database;
using backend.Services.Authentication.Login;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services.Email;

public interface IPasswordResetService
{
    Task RequestResetAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ResetPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default);
}

public sealed class PasswordResetService(
    TukiDbContext context,
    IEmailSender emailSender,
    ILocalAuthenticationService localAuthenticationService,
    IOptions<EmailOptions> options) : IPasswordResetService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(30);
    private readonly EmailOptions _options = options.Value;

    public async Task RequestResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();
        var user = await context.UserProfiles
            .FirstOrDefaultAsync(profile => profile.Email == normalizedEmail && profile.IsActive, cancellationToken);
        if (user is null)
        {
            // Do not reveal whether the account exists.
            return;
        }

        var code = VerificationCode.Generate();
        context.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.UserId,
            TokenHash = VerificationCode.Hash(code),
            ExpiresAt = DateTime.UtcNow.Add(CodeLifetime),
        });
        await context.SaveChangesAsync(cancellationToken);

        var subject = $"Reset your {_options.AppDisplayName} password";
        var html =
            $"<p>Your {_options.AppDisplayName} password reset code is:</p>" +
            $"<p style=\"font-size:24px;font-weight:bold;letter-spacing:4px;\">{code}</p>" +
            $"<p>This code expires in {CodeLifetime.TotalMinutes:0} minutes. If you did not request this, you can ignore this email.</p>";

        await emailSender.SendAsync(user.Email, subject, html, $"Your password reset code is {code}.", cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();
        var hash = VerificationCode.Hash(code);
        var now = DateTime.UtcNow;

        var token = await context.PasswordResetTokens
            .Include(t => t.User)
            .Where(t => t.TokenHash == hash && t.ConsumedAt == null && t.ExpiresAt > now)
            .FirstOrDefaultAsync(cancellationToken);
        if (token is null || !string.Equals(token.User.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token.ConsumedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        await localAuthenticationService.StoreCredentialAsync(token.UserId, newPassword, cancellationToken);
        return true;
    }
}
