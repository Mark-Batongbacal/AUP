using Google.Apis.Auth;

namespace backend.Services.Authentication.Google;

public interface IGoogleIdTokenValidator
{
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, string audience);
}

public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    public Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, string audience)
    {
        var validationSettings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [audience]
        };

        return GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);
    }
}
