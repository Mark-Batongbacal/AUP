namespace Tuki.Admin.Models.Auth;

public sealed class LoginResponse
{
    public string ApiKey { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public string AuthenticationScheme { get; init; } = "ApiKey";
    public string HeaderName { get; init; } = "X-Api-Key";
}
