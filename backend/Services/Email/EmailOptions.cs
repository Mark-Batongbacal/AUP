namespace backend.Services.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public const string AzureConnectionStringEnvironmentVariable = "COMMUNICATION_SERVICES_CONNECTION_STRING";

    public string ConnectionString { get; set; } = string.Empty;

    public string SenderAddress { get; set; } = "DoNotReply@tuki.pawfect.bar";

    public string AppDisplayName { get; set; } = "TUKI";

    public string ResolveConnectionString() =>
        !string.IsNullOrWhiteSpace(ConnectionString)
            ? ConnectionString
            : Environment.GetEnvironmentVariable(AzureConnectionStringEnvironmentVariable) ?? string.Empty;

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(ResolveConnectionString()) && !string.IsNullOrWhiteSpace(SenderAddress);
}
