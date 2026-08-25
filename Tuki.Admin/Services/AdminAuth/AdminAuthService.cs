using Tuki.Admin.Models.Auth;
using Tuki.Admin.Services.BackendApiClient;

namespace Tuki.Admin.Services.AdminAuth;

public sealed class AdminAuthService(IBackendApiClientService backendApiClient) : IAdminAuthService
{
    public async Task<AdminAuthenticationResult> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserName = userName.Trim();

        LoginResponse? login;
        try
        {
            login = await backendApiClient.LoginAsync(
                new LoginRequest
                {
                    UserName = normalizedUserName,
                    Password = password
                },
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new AdminAuthenticationResult(
                false,
                "The TUKI backend is currently unavailable. Start the backend and try again.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AdminAuthenticationResult(false, "The TUKI backend did not respond in time.");
        }

        if (login is null)
        {
            return new AdminAuthenticationResult(false, "Invalid administrator username or password.");
        }

        return new AdminAuthenticationResult(true, UserName: normalizedUserName, Login: login);
    }
}
