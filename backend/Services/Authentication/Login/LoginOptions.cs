namespace backend.Services.Authentication.Login;

public sealed class LoginOptions
{
    public const string SectionName = "Login";

    public List<LoginUserOptions> Users { get; init; } = [];

    // Kept temporarily so deployments using the original environment variables continue to work.
    public string InitialUserName { get; init; } = string.Empty;
    public string InitialPassword { get; init; } = string.Empty;
    public int ApiKeyLifetimeHours { get; init; } = 8;

    public IEnumerable<LoginUserOptions> ConfiguredUsers =>
        Users.Count > 0
            ? Users
            : [new LoginUserOptions { UserName = InitialUserName, Password = InitialPassword }];
}

public sealed class LoginUserOptions
{
    public string UserName { get; init; } = string.Empty;
    
    public string Password { get; init; } = string.Empty;
}
