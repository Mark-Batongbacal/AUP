namespace backend.Services.Localization;

public static class TukiLanguage
{
    public const string English = "English";
    public const string Filipino = "Filipino";

    public static string Normalize(string? language) =>
        string.Equals(language?.Trim(), Filipino, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(language?.Trim(), "Tagalog", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(language?.Trim(), "fil", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(language?.Trim(), "tl", StringComparison.OrdinalIgnoreCase)
            ? Filipino
            : English;

    public static bool IsFilipino(string? language) =>
        string.Equals(Normalize(language), Filipino, StringComparison.Ordinal);
}
