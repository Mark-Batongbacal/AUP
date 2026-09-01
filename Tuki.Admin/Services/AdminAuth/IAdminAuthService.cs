using Tuki.Admin.Models.Auth;

namespace Tuki.Admin.Services.AdminAuth;

public interface IAdminAuthService
{
    Task<AdminAuthenticationResult> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default);
}

public sealed record AdminAuthenticationResult(
    bool Succeeded,
    string? ErrorMessage = null,
    string? UserName = null,
    LoginResponse? Login = null);
