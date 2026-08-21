using backend.Models.Database;
using backend.Services.Authentication.Login;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services.Email;

public interface IPasswordResetService
{
    Task RequestResetAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<bool> RequestPasswordChangeAsync(
        Guid userId,
        string currentPassword,
        CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public sealed class PasswordResetService(
    TukiDbContext context,
    IEmailSender emailSender,
    ILocalAuthenticationService localAuthenticationService,
    IOptions<EmailOptions> options) : IPasswordResetService
{
    private const string ResetPurpose = "Reset";
    private const string ChangePurpose = "Change";
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ChangeCodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan OtpSendCooldown = TimeSpan.FromMinutes(3);
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

        var code = await CreateOtpAsync(user.UserId, ResetPurpose, ResetCodeLifetime, cancellationToken);
        if (code is null)
        {
            // Keep the public response generic while enforcing the send cooldown server-side.
            return;
        }

        var subject = $"Reset your {_options.AppDisplayName} password";
        var html = BuildOtpEmail(
            "Password reset",
            $"Use this code to reset your {_options.AppDisplayName} password:",
            code,
            ResetCodeLifetime,
            "If you did not request a password reset, you can safely ignore this email.");

        await emailSender.SendAsync(
            user.Email,
            subject,
            html,
            $"Your {_options.AppDisplayName} password reset code is {code}. It expires in {ResetCodeLifetime.TotalMinutes:0} minutes.",
            cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();
        var hash = VerificationCode.Hash(code.Trim());
        var now = DateTime.UtcNow;

        var token = await context.PasswordResetTokens
            .Include(t => t.User)
            .Where(t =>
                t.Purpose == ResetPurpose &&
                t.TokenHash == hash &&
                t.ConsumedAt == null &&
                t.ExpiresAt > now)
            .FirstOrDefaultAsync(cancellationToken);
        if (token is null || !string.Equals(token.User.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        await ConsumeOutstandingOtpsAsync(token.UserId, now, cancellationToken);
        await localAuthenticationService.StoreCredentialAsync(token.UserId, newPassword, cancellationToken);
        return true;
    }

    public async Task<bool> RequestPasswordChangeAsync(
        Guid userId,
        string currentPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == userId && profile.IsActive, cancellationToken);
        if (user is null ||
            !localAuthenticationService.CredentialsAreValid(user.Email, currentPassword))
        {
            return false;
        }

        var code = await CreateOtpAsync(user.UserId, ChangePurpose, ChangeCodeLifetime, cancellationToken);
        if (code is null)
        {
            // The password was valid, but a code was already sent recently.
            return true;
        }

        var subject = $"Confirm your {_options.AppDisplayName} password change";
        var html = BuildOtpEmail(
            "Confirm password change",
            $"Use this code to confirm the password change for your {_options.AppDisplayName} account:",
            code,
            ChangeCodeLifetime,
            "If you did not request this change, do not share this code and keep your current password.");

        await emailSender.SendAsync(
            user.Email,
            subject,
            html,
            $"Your {_options.AppDisplayName} password change code is {code}. It expires in {ChangeCodeLifetime.TotalMinutes:0} minutes.",
            cancellationToken);

        return true;
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == userId && profile.IsActive, cancellationToken);
        if (user is null ||
            !localAuthenticationService.CredentialsAreValid(user.Email, currentPassword))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var hash = VerificationCode.Hash(code.Trim());
        var token = await context.PasswordResetTokens
            .Where(t =>
                t.UserId == userId &&
                t.Purpose == ChangePurpose &&
                t.TokenHash == hash &&
                t.ConsumedAt == null &&
                t.ExpiresAt > now)
            .FirstOrDefaultAsync(cancellationToken);
        if (token is null)
        {
            return false;
        }

        await ConsumeOutstandingOtpsAsync(userId, now, cancellationToken);
        await localAuthenticationService.StoreCredentialAsync(userId, newPassword, cancellationToken);
        return true;
    }

    private async Task<string?> CreateOtpAsync(
        Guid userId,
        string purpose,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var cooldownCutoff = now.Subtract(OtpSendCooldown);
        var recentlySent = await context.PasswordResetTokens
            .AsNoTracking()
            .AnyAsync(token =>
                token.UserId == userId &&
                token.Purpose == purpose &&
                token.CreatedAt > cooldownCutoff,
                cancellationToken);

        if (recentlySent)
        {
            return null;
        }

        var previousTokens = await context.PasswordResetTokens
            .Where(token =>
                token.UserId == userId &&
                token.Purpose == purpose &&
                token.ConsumedAt == null &&
                token.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in previousTokens)
        {
            token.ConsumedAt = now;
        }

        var code = VerificationCode.Generate();
        context.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = userId,
            TokenHash = VerificationCode.Hash(code),
            Purpose = purpose,
            ExpiresAt = now.Add(lifetime),
            CreatedAt = now,
        });
        await context.SaveChangesAsync(cancellationToken);

        return code;
    }

    private async Task ConsumeOutstandingOtpsAsync(
        Guid userId,
        DateTime consumedAt,
        CancellationToken cancellationToken)
    {
        var activeTokens = await context.PasswordResetTokens
            .Where(token => token.UserId == userId && token.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.ConsumedAt = consumedAt;
        }
    }

    private static string BuildOtpEmail(
        string heading,
        string introduction,
        string code,
        TimeSpan lifetime,
        string footer) =>
        $"<div style=\"font-family:Arial,sans-serif;max-width:520px;margin:auto;padding:24px;color:#1f2937;\">" +
        $"<h2 style=\"margin-bottom:8px;\">{heading}</h2>" +
        $"<p>{introduction}</p>" +
        $"<div style=\"font-size:30px;font-weight:700;letter-spacing:8px;margin:24px 0;\">{code}</div>" +
        $"<p>This code expires in {lifetime.TotalMinutes:0} minutes.</p>" +
        $"<p style=\"color:#6b7280;font-size:14px;\">{footer}</p>" +
        "</div>";
}
