namespace backend.Services.Authentication.Facebook;

public sealed class FacebookOptions
{
    public const string SectionName = "Facebook";
    public const string DefaultOidcIssuer = "https://www.facebook.com";
    public const string DefaultOidcJwksUri = "https://www.facebook.com/.well-known/oauth/openid/jwks/";

    public string AppId { get; init; } = string.Empty;

    public string AppSecret { get; init; } = string.Empty;

    public string OidcIssuer { get; init; } = DefaultOidcIssuer;

    public string OidcJwksUri { get; init; } = DefaultOidcJwksUri;
}
