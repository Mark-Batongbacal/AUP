namespace backend.Services.Authentication.Facebook;

public interface IFacebookOidcTokenValidator
{
    Task<FacebookOidcUserInfo> ValidateAsync(
        string idToken,
        string appId,
        string nonce,
        CancellationToken cancellationToken = default);
}

public sealed record FacebookOidcUserInfo(string Subject, string? Name, string? Email);

public sealed class FacebookOidcTokenValidationException : Exception
{
    public FacebookOidcTokenValidationException()
        : base("The Facebook OIDC token is invalid.")
    {
    }

    public FacebookOidcTokenValidationException(Exception innerException)
        : base("The Facebook OIDC token is invalid.", innerException)
    {
    }
}

public sealed class FacebookOidcTokenValidationUnavailableException : Exception
{
    public FacebookOidcTokenValidationUnavailableException()
        : base("Facebook OIDC token validation is unavailable.")
    {
    }

    public FacebookOidcTokenValidationUnavailableException(Exception innerException)
        : base("Facebook OIDC token validation is unavailable.", innerException)
    {
    }
}
