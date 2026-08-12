namespace backend.Services;

public sealed class LoginOptions
{
    public const string SectionName = "Login";

    public string InitialUserName { get; init; } = string.Empty;
    public string InitialPassword { get; init; } = string.Empty;
    public int ApiKeyLifetimeHours { get; init; } = 8;
}
