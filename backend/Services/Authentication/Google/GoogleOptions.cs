namespace backend.Services.Authentication.Google;

public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; init; } = string.Empty;
}
