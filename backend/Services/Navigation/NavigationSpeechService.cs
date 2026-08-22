namespace backend.Services.Navigation;

public sealed record NavigationSpeechContext(
    string InstructionType,
    string State,
    string? TransportMode = null,
    string? RouteName = null,
    string? LandmarkName = null,
    string? LandmarkRole = null,
    string? LandmarkRelation = null,
    double? DistanceMeters = null,
    string? Status = null,
    bool UseDynamicDistance = false);

public interface INavigationSpeechService
{
    Task<string> PhraseAsync(
        NavigationSpeechContext context,
        CancellationToken cancellationToken = default);
}

// Kept under the existing class name so current DI wiring and tests remain compatible.
// Navigation speech is intentionally local and deterministic: the hot path must never
// wait on an external LLM/provider just to say the next navigation instruction.
public sealed class NemotronNavigationSpeechService : INavigationSpeechService
{
    public Task<string> PhraseAsync(
        NavigationSpeechContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DeterministicNavigationSpeech.Phrase(context));
    }
}

public static class NavigationSpeechTemplate
{
    public const string DistanceToken = "{distance}";

    public static string Normalize(string? template, NavigationSpeechContext context)
    {
        var value = template?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return DeterministicNavigationSpeech.Phrase(context);

        if (context.UseDynamicDistance)
        {
            var tokenCount = value.Split(DistanceToken, StringSplitOptions.None).Length - 1;
            if (tokenCount != 1)
                return DeterministicNavigationSpeech.Phrase(context);
        }
        else if (value.Contains(DistanceToken, StringComparison.Ordinal))
        {
            return DeterministicNavigationSpeech.Phrase(context);
        }

        return value;
    }

    public static string Render(string? template, double? distanceMeters)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;
        if (!template.Contains(DistanceToken, StringComparison.Ordinal)) return template;
        var distance = FormatDistance(distanceMeters);
        return template.Replace(DistanceToken, distance, StringComparison.Ordinal);
    }

    public static string FormatDistance(double? distanceMeters)
    {
        var safe = Math.Max(0, distanceMeters ?? 0);
        if (safe >= 1_000)
            return $"{safe / 1_000d:0.#} km";

        var bucket = safe switch
        {
            >= 500 => 100d,
            >= 200 => 50d,
            >= 100 => 25d,
            _ => 10d
        };
        var rounded = Math.Max(bucket, Math.Round(safe / bucket) * bucket);
        return $"{rounded:0}m";
    }
}

public static class DeterministicNavigationSpeech
{
    public static string Phrase(NavigationSpeechContext context)
    {
        var route = string.IsNullOrWhiteSpace(context.RouteName)
            ? null
            : context.RouteName.Trim();
        var mode = context.TransportMode?.Trim().ToUpperInvariant();
        var isWalking = mode is "WALK" or "WALKING" or "PEDESTRIAN";

        return context.InstructionType switch
        {
            "BoardJeepney" => context.LandmarkName is { Length: > 0 } landmark
                ? route is { Length: > 0 }
                    ? $"Sakay tayo ng {route} jeep malapit sa {landmark}."
                    : $"Sakay tayo ng jeep malapit sa {landmark}."
                : route is { Length: > 0 }
                    ? $"Sakay tayo ng {route} jeep dito."
                    : "Sakay tayo ng jeep dito.",

            "BoardTricycle" => context.LandmarkName is { Length: > 0 } landmark
                ? $"Sakay tayo ng tricycle malapit sa {landmark}."
                : "Sakay tayo ng tricycle dito.",

            "PrepareToAlight" when context.UseDynamicDistance =>
                $"Konti na lang—mga {NavigationSpeechTemplate.DistanceToken} na lang bago tayo bumaba.",

            "PrepareToAlight" => context.LandmarkName is { Length: > 0 } landmark
                ? $"Malapit na tayong bumaba. Lampasan muna natin ang {landmark}."
                : "Malapit na tayong bumaba. Maghanda na tayo.",

            "AlightJeepney" or "AlightTricycle" => "Dito na tayo bababa.",

            "LandmarkNotice" => context.LandmarkName is { Length: > 0 } landmark
                ? $"Ayun, nalampasan na natin ang {landmark}."
                : "Diretso lang muna tayo.",

            "Transfer" => "Baba tayo dito, tapos tuloy tayo sa next ride.",
            "Arrived" => "Ayun, nandito na tayo!",
            "Cancelled" => "Okay, cancelled na yung navigation.",
            "MissedAlight" => "Mukhang lumagpas tayo sa babaan. I-check natin yung next route.",
            "OffRoute" => "Mukhang wala na tayo sa planned route. I-check natin yung next step.",
            "Rerouted" => "Okay, updated na yung route. Sundan natin yung next instruction.",
            "TurnLeft" => "Kaliwa tayo dito.",
            "TurnRight" => "Kanan tayo dito.",

            _ when context.UseDynamicDistance && isWalking && context.DistanceMeters is <= 500 =>
                $"Sige, lakad pa tayo nang {NavigationSpeechTemplate.DistanceToken}. Konti na lang!",

            _ when context.UseDynamicDistance && isWalking =>
                $"Sige, lakad pa tayo nang {NavigationSpeechTemplate.DistanceToken}.",

            _ when context.UseDynamicDistance =>
                $"Diretso lang muna tayo, mga {NavigationSpeechTemplate.DistanceToken} pa.",

            _ => "Diretso lang muna tayo sa route."
        };
    }
}
