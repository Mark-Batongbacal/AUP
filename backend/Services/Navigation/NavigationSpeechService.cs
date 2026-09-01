using System.Security.Claims;
using System.Text.Json;
using backend.Repositories;
using backend.Services.Localization;
using backend.Services.Telemetry;
using OpenAI;
using OpenAI.Chat;

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
    bool UseDynamicDistance = false,
    string Language = TukiLanguage.English);

public interface INavigationSpeechService
{
    Task<string> PhraseAsync(
        NavigationSpeechContext context,
        CancellationToken cancellationToken = default);
}

// Gemini phrases meaningful navigation events, while deterministic speech is always
// available as the safety/latency fallback.
public sealed class GeminiNavigationSpeechService(
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    IUserProfileRepository userProfiles,
    ILogger<GeminiNavigationSpeechService> logger,
    IAiUsageMetricsStore aiUsageMetrics)
    : INavigationSpeechService
{
    private const string DisableAiHeader = "X-Tuki-Disable-Ai";
    private static readonly TimeSpan GeminiTimeout = TimeSpan.FromSeconds(15);

    public async Task<string> PhraseAsync(
        NavigationSpeechContext context,
        CancellationToken cancellationToken = default)
    {
        var language = await ResolveLanguageAsync(cancellationToken);
        var localizedContext = context with { Language = language };

        // Capacity/load tests can explicitly request deterministic navigation
        // speech so they measure Tuki's infrastructure without consuming
        // Gemini quota or mixing external-model latency into the benchmark.
        // This only changes wording for the current request and does not bypass
        // authentication, routing, navigation state, or any safety checks.
        if (IsAiDisabledForRequest())
            return DeterministicNavigationSpeech.Phrase(localizedContext);

        var apiKey = Environment.GetEnvironmentVariable(
            configuration["Gemini:ApiKeyEnvironmentVariable"] ??
            "GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return DeterministicNavigationSpeech.Phrase(localizedContext);

        var model = configuration["Gemini:Model"] ?? "gemini-3.5-flash-lite";
        var client = new ChatClient(
            model,
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(configuration["Gemini:BaseUrl"] ??
                    "https://generativelanguage.googleapis.com/v1beta/openai/")
            });

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(GeminiTimeout);

        try
        {
            var response = await client.CompleteChatAsync(
            [
                new SystemChatMessage(PromptFor(language)),
                new UserChatMessage(JsonSerializer.Serialize(localizedContext))
            ], cancellationToken: timeout.Token);

            var usage = response.Value.Usage;
            aiUsageMetrics.Record(
                "navigation",
                model,
                usage?.InputTokenCount ?? 0,
                usage?.OutputTokenCount ?? 0);

            var text = response.Value.Content.FirstOrDefault()?.Text?.Trim();
            return NavigationSpeechTemplate.Normalize(text, localizedContext);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Gemini navigation speech exceeded {TimeoutSeconds}s; using deterministic fallback",
                GeminiTimeout.TotalSeconds);
            return DeterministicNavigationSpeech.Phrase(localizedContext);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Gemini navigation speech unavailable; using deterministic fallback");
            return DeterministicNavigationSpeech.Phrase(localizedContext);
        }
    }

    private bool IsAiDisabledForRequest()
    {
        var value = httpContextAccessor.HttpContext?
            .Request.Headers[DisableAiHeader]
            .ToString();

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
               value == "1";
    }

    private async Task<string> ResolveLanguageAsync(CancellationToken cancellationToken)
    {
        var userIdText = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdText, out var userId))
            return TukiLanguage.English;

        try
        {
            var profile = await userProfiles.GetActiveByUserIdAsync(userId, cancellationToken);
            return TukiLanguage.Normalize(profile?.PreferredLanguage);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not load navigation language preference; defaulting to English");
            return TukiLanguage.English;
        }
    }

    private static string PromptFor(string language) =>
        TukiLanguage.IsFilipino(language)
            ? """
                You are Tuki, a cheerful Filipino commute buddy and friendly toucan.

                LANGUAGE RULE — FILIPINO:
                - Your entire spoken response must be natural conversational Filipino/Taglish.
                - Prefer Filipino sentence structure and tayo/natin/mo/ka.
                - English is allowed only for normal commute/app terms such as route, destination, ETA, jeep, tricycle, TODA, street names, and proper nouns.
                - Do not switch to a fully English sentence.

                Write exactly ONE short spoken navigation sentence.
                Sound like a helpful local friend traveling with the user. Keep it warm and clear, never formal, robotic, or customer-service-like.

                Use ONLY facts in the JSON. Never invent routes, landmarks, turns, stops, fares, distances, modes, or events.
                Preserve route and landmark names exactly in meaning. Do not expose technical state names.
                If UseDynamicDistance is true, include the literal token {distance} exactly once where the changing distance belongs; never print DistanceMeters yourself.
                If UseDynamicDistance is false, do not use {distance} and do not infer a distance.
                Return plain text only.

                Natural tone examples only:
                "Sige, lakad pa tayo nang {distance}. Konti na lang!"
                "Mga {distance} pa, tapos baba na tayo."
                "Kaliwa tayo dito."
                "Ayun, nandito na tayo!"
                """
            : """
                You are Tuki, a cheerful commute buddy and friendly toucan.

                LANGUAGE RULE — ENGLISH:
                - Your entire spoken response must be English only.
                - Do NOT use Filipino, Tagalog, or Taglish words or sentence structure.
                - This rule applies even if the user's message, place context, or previous text contains Filipino.
                - Proper nouns, route names, street names, and Filipino place names must remain unchanged.

                Write exactly ONE short spoken navigation sentence.
                Sound warm, concise, encouraging, and clear, like a helpful friend traveling with the user. Never sound formal, robotic, or like customer support.

                Use ONLY facts in the JSON. Never invent routes, landmarks, turns, stops, fares, distances, modes, or events.
                Preserve route and landmark names exactly in meaning. Do not expose technical state names.
                If UseDynamicDistance is true, include the literal token {distance} exactly once where the changing distance belongs; never print DistanceMeters yourself.
                If UseDynamicDistance is false, do not use {distance} and do not infer a distance.
                Return plain text only.

                Natural tone examples only:
                "Keep walking for {distance}. Almost there!"
                "About {distance} more, then get ready to hop off."
                "Turn left here."
                "We're here!"
                """;
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
    public static string Phrase(NavigationSpeechContext context) =>
        TukiLanguage.IsFilipino(context.Language)
            ? PhraseFilipino(context)
            : PhraseEnglish(context);

    private static string PhraseFilipino(NavigationSpeechContext context)
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
            "AlightStatusUnknown" => "Nakapagbaba ka na ba? Piliin kung nakababa ka na o nasa jeep ka pa.",
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

    private static string PhraseEnglish(NavigationSpeechContext context)
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
                    ? $"Let's take the {route} jeep near {landmark}."
                    : $"Let's take the jeep near {landmark}."
                : route is { Length: > 0 }
                    ? $"Let's take the {route} jeep here."
                    : "Let's take the jeep here.",

            "BoardTricycle" => context.LandmarkName is { Length: > 0 } landmark
                ? $"Let's take the tricycle near {landmark}."
                : "Let's take the tricycle here.",

            "PrepareToAlight" when context.UseDynamicDistance =>
                $"Almost there—get ready to hop off in about {NavigationSpeechTemplate.DistanceToken}.",

            "PrepareToAlight" => context.LandmarkName is { Length: > 0 } landmark
                ? $"We're getting close. Get ready to hop off after {landmark}."
                : "We're getting close. Get ready to hop off.",

            "AlightJeepney" or "AlightTricycle" => "This is our stop. Let's get off here.",

            "LandmarkNotice" => context.LandmarkName is { Length: > 0 } landmark
                ? $"We just passed {landmark}."
                : "Keep going on the current route.",

            "Transfer" => "Let's get off here, then continue to the next ride.",
            "Arrived" => "We're here!",
            "Cancelled" => "Okay, navigation is cancelled.",
            "MissedAlight" => "Looks like we passed the stop. Let's check the next route.",
            "AlightStatusUnknown" => "Did you already get off? Choose whether you're off or still riding.",
            "OffRoute" => "Looks like we're off the planned route. Let's check the next step.",
            "Rerouted" => "Route updated. Let's follow the next instruction.",
            "TurnLeft" => "Turn left here.",
            "TurnRight" => "Turn right here.",

            _ when context.UseDynamicDistance && isWalking && context.DistanceMeters is <= 500 =>
                $"Keep walking for {NavigationSpeechTemplate.DistanceToken}. Almost there!",

            _ when context.UseDynamicDistance && isWalking =>
                $"Keep walking for {NavigationSpeechTemplate.DistanceToken}.",

            _ when context.UseDynamicDistance =>
                $"Keep going for about {NavigationSpeechTemplate.DistanceToken}.",

            _ => "Keep going on the planned route."
        };
    }
}
