namespace backend.Services.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string ConnectionString { get; set; } = string.Empty;

    public string SenderAddress { get; set; } = string.Empty;

    public string AppDisplayName { get; set; } = "TUKI";

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(ConnectionString) && !string.IsNullOrWhiteSpace(SenderAddress);
}
