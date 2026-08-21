using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using backend.Authentication;
using backend.Models.Database;
using backend.Services;
using backend.Services.Authentication.ApiKey;
using backend.Services.Authentication.Login;
using backend.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class OtpAuthController(
    TukiDbContext context,
    IEmailSender emailSender,
    ILocalAuthenticationService localAuthenticationService,
    IUserProfileService userProfileService,
    IApiKeyService apiKeyService,
    IOptions<EmailOptions> emailOptions) : ControllerBase
{
    private const string ResetPurpose = "Reset";
    private const string ChangePurpose = "Change";
    private static readonly TimeSpan RegistrationCodeLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan OtpSendCooldown = TimeSpan.FromMinutes(3);
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    [HttpPost("register/request-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestRegistrationOtp(
        RegistrationOtpRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (await context.UserProfiles.AsNoTracking().AnyAsync(user => user.Email == email, cancellationToken))
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        if (!RegistrationOtpStore.CanSend(email, OtpSendCooldown))
        {
            return Ok(new { message = "A verification code was already sent recently. Please wait before requesting another." });
        }

        var code = VerificationCode.Generate();
        RegistrationOtpStore.Set(
            email,
            VerificationCode.Hash(code),
            DateTime.UtcNow.Add(RegistrationCodeLifetime));

        var html = BuildOtpEmail(
            "Verify your email",
            $"Use this code to continue creating your {_emailOptions.AppDisplayName} account:",
            code,
            RegistrationCodeLifetime,
            "If you did not try to create this account, you can safely ignore this email.");

        await emailSender.SendAsync(
            email,
            $"Verify your {_emailOptions.AppDisplayName} email",
            html,
            $"Your {_emailOptions.AppDisplayName} registration code is {code}. It expires in {RegistrationCodeLifetime.TotalMinutes:0} minutes.",
            cancellationToken);

        return Ok(new { message = "Verification code sent." });
    }

    [HttpPost("register/verify-otp")]
    [AllowAnonymous]
    public IActionResult VerifyRegistrationOtp(RegistrationOtpVerifyRequest request)
    {
        var verified = RegistrationOtpStore.IsValid(
            NormalizeEmail(request.Email),
            VerificationCode.Hash(request.Code));

        return verified
            ? Ok(new { message = "Email verified. Continue creating your password." })
            : BadRequest(new { message = "The verification code is invalid or has expired." });
    }

    [HttpPost("register/complete")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResponse>> CompleteRegistration(
        CompleteRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.UserName);
        if (!RegistrationOtpStore.Consume(email, VerificationCode.Hash(request.VerificationCode)))
        {
            return BadRequest(new { message = "Verify your email with a valid code before registering." });
        }

        var registration = await userProfileService.RegisterLocalProfileAsync(
            email,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            cancellationToken);

        if (registration.Status == UserProfileRegistrationStatus.Duplicate)
        {
            return Conflict(new { message = registration.Errors[0] });
        }

        if (registration.Status == UserProfileRegistrationStatus.ValidationFailed)
        {
            return BadRequest(new { errors = registration.Errors });
        }

        var authentication = registration.Authentication!;
        await localAuthenticationService.StoreCredentialAsync(
            authentication.UserId,
            request.Password,
            cancellationToken);

        var profile = await context.UserProfiles
            .FirstAsync(user => user.UserId == authentication.UserId, cancellationToken);
        profile.IsEmailVerified = true;
        profile.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        var issuedKey = apiKeyService.Create(authentication.CredentialOwner);
        return StatusCode(StatusCodes.Status201Created, new RegisterResponse(
            authentication.UserId,
            authentication.CredentialOwner,
            authentication.Profile.FirstName,
            authentication.Profile.LastName,
            issuedKey.Value,
            issuedKey.ExpiresAt));
    }

    [HttpPost("forgot-password/verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyResetOtp(
        PasswordOtpVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var hash = VerificationCode.Hash(request.Code);
        var now = DateTime.UtcNow;

        var valid = await context.PasswordResetTokens
            .AsNoTracking()
            .Include(token => token.User)
            .AnyAsync(token =>
                token.Purpose == ResetPurpose &&
                token.TokenHash == hash &&
                token.ConsumedAt == null &&
                token.ExpiresAt > now &&
                token.User.Email == email,
                cancellationToken);

        return valid
            ? Ok(new { message = "Code verified." })
            : BadRequest(new { message = "The reset code is invalid or has expired." });
    }

    [HttpPost("change-password/verify-otp")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    public async Task<IActionResult> VerifyChangePasswordOtp(
        ChangePasswordVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = UserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var user = await context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == userId && profile.IsActive, cancellationToken);
        if (user is null || !localAuthenticationService.CredentialsAreValid(user.Email, request.CurrentPassword))
        {
            return BadRequest(new { message = "The current password is incorrect." });
        }

        var hash = VerificationCode.Hash(request.Code);
        var now = DateTime.UtcNow;
        var valid = await context.PasswordResetTokens
            .AsNoTracking()
            .AnyAsync(token =>
                token.UserId == userId &&
                token.Purpose == ChangePurpose &&
                token.TokenHash == hash &&
                token.ConsumedAt == null &&
                token.ExpiresAt > now,
                cancellationToken);

        return valid
            ? Ok(new { message = "Code verified." })
            : BadRequest(new { message = "The confirmation code is invalid or has expired." });
    }

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

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

public sealed record RegistrationOtpRequest(
    [Required, EmailAddress, StringLength(255)] string Email);

public sealed record RegistrationOtpVerifyRequest(
    [Required, EmailAddress, StringLength(255)] string Email,
    [Required, RegularExpression("^[0-9]{8}$")] string Code);

public sealed record CompleteRegistrationRequest(
    [Required, EmailAddress, StringLength(255)] string UserName,
    [Required, StringLength(256, MinimumLength = 8)] string Password,
    [Required, StringLength(100, MinimumLength = 1)] string FirstName,
    [Required, StringLength(100, MinimumLength = 1)] string LastName,
    [Required, RegularExpression("^[0-9]{8}$")] string VerificationCode,
    [StringLength(30)] string? PhoneNumber = null);

public sealed record PasswordOtpVerifyRequest(
    [Required, EmailAddress, StringLength(255)] string Email,
    [Required, RegularExpression("^[0-9]{8}$")] string Code);

public sealed record ChangePasswordVerifyRequest(
    [Required, StringLength(256, MinimumLength = 8)] string CurrentPassword,
    [Required, RegularExpression("^[0-9]{8}$")] string Code);

internal static class RegistrationOtpStore
{
    private sealed record Entry(string Hash, DateTime ExpiresAt, DateTime SentAt);

    private static readonly ConcurrentDictionary<string, Entry> Pending =
        new(StringComparer.OrdinalIgnoreCase);

    public static bool CanSend(string email, TimeSpan cooldown)
    {
        if (!Pending.TryGetValue(email, out var entry))
        {
            return true;
        }

        var now = DateTime.UtcNow;
        if (entry.ExpiresAt <= now)
        {
            Pending.TryRemove(email, out _);
            return true;
        }

        return now - entry.SentAt >= cooldown;
    }

    public static void Set(string email, string hash, DateTime expiresAt) =>
        Pending[email] = new Entry(hash, expiresAt, DateTime.UtcNow);

    public static bool IsValid(string email, string hash)
    {
        if (!Pending.TryGetValue(email, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            Pending.TryRemove(email, out _);
            return false;
        }

        return string.Equals(entry.Hash, hash, StringComparison.Ordinal);
    }

    public static bool Consume(string email, string hash)
    {
        if (!IsValid(email, hash))
        {
            return false;
        }

        return Pending.TryRemove(email, out _);
    }
}
