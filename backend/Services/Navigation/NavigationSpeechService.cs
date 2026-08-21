using System.Text.Json;
using backend.Models.Database;
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
    string? Status = null);

public interface INavigationSpeechService
{
    Task<string> PhraseAsync(
        NavigationSpeechContext context,
        CancellationToken cancellationToken = default);
}

// Kept under the existing class name so current DI wiring and tests remain compatible.
// The conversational/navigation voice is now handled by Qwen; Nemotron remains the intent parser.
public sealed class NemotronNavigationSpeechService(IConfiguration configuration)
    : INavigationSpeechService
{
    public async Task<string> PhraseAsync(
        NavigationSpeechContext context,
        CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(
            configuration["Qwen:ApiKeyEnvironmentVariable"] ??
            configuration["Nvidia:ApiKeyEnvironmentVariable"] ??
            "NVIDIA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("The configured Qwen API key is unavailable.");

        var client = new ChatClient(
            configuration["Qwen:Model"] ?? "qwen/qwen3-next-80b-a3b-instruct",
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(configuration["Qwen:BaseUrl"] ??
                    configuration["Nvidia:BaseUrl"] ??
                    "https://integrate.api.nvidia.com/v1")
            });
        var response = await client.CompleteChatAsync(
        [
            new SystemChatMessage("""
                You are Tuki, a cheerful Filipino commute buddy and friendly toucan.
                Write exactly one short navigation sentence that sounds natural when spoken aloud.

                VOICE:
                - Warm, energetic, encouraging, and conversational.
                - Use natural Taglish when it fits the supplied context.
                - Friendly expressions such as "Tara!", "Ayun!", "Sige!", and "Konti na lang!" are welcome when appropriate.
                - Sound like a helpful local friend riding with the user, never a customer-service agent or GPS robot.
                - Keep Filipino commute words natural: sakay, baba, lakad, tawid, kanto, terminal, jeep, TODA.
                - Do not force slang or hype into every instruction. Safety and clarity come first.

                SAFETY / GROUNDING:
                - Use ONLY facts present in the supplied JSON.
                - Never invent a route, landmark, direction, stop, fare, distance, transport mode, or event.
                - Preserve every supplied route name, landmark name, direction, and distance exactly in meaning.
                - Do not expose technical state names.
                - If the JSON does not support a detail, do not mention it.
                - Return plain text only, with no quotes, JSON, markdown, or explanation.

                Examples of tone only (never copy facts from them):
                "Tara! Lakad ka muna mga 2 minutes papunta sa sakayan."
                "Ayun, malapit na! Baba ka sa next planned stop."
                "Sige, diretso lang muna — sasabihan kita pag malapit na."
                "YESS, nandito na tayo! Ingat sa pagbaba."
                """),
            new UserChatMessage(JsonSerializer.Serialize(context))
        ], cancellationToken: cancellationToken);
        var text = response.Value.Content.FirstOrDefault()?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Navigation speech provider returned no text.");
        return text;
    }
}

public static class DeterministicNavigationSpeech
{
    public static string Phrase(NavigationSpeechContext context)
    {
        var route = string.IsNullOrWhiteSpace(context.RouteName)
            ? "the selected ride" : context.RouteName;
        return context.InstructionType switch
        {
            "BoardJeepney" => context.LandmarkName is { Length: > 0 } landmark
                ? $"Board the {route} jeep near {landmark}."
                : $"Board the {route} jeep here.",
            "BoardTricycle" => context.LandmarkName is { Length: > 0 } landmark
                ? $"Board the tricycle near {landmark}."
                : "Board the tricycle here.",
            "PrepareToAlight" => context.LandmarkName is { Length: > 0 } landmark
                ? $"Prepare to get off after you pass {landmark}."
                : "Prepare to get off soon.",
            "AlightJeepney" or "AlightTricycle" => "Get off here.",
            "LandmarkNotice" => context.LandmarkName is { Length: > 0 } landmark
                ? $"You just passed {landmark}." : "Continue on the current route.",
            "Transfer" => "Get off here and continue to your next ride.",
            "Arrived" => "You have arrived.",
            "Cancelled" => "Navigation is cancelled.",
            "MissedAlight" => "It looks like you passed the stop; Tuki is finding a new route.",
            "OffRoute" => "You are off the planned route; Tuki is finding a new route.",
            "Rerouted" => "Your route is updated; follow the next instruction.",
            "TurnLeft" => "Turn left here.",
            "TurnRight" => "Turn right here.",
            _ when context.DistanceMeters is { } distance && distance > 0 =>
                $"Continue for about {Math.Round(distance):0} meters.",
            _ => "Continue along the planned route."
        };
    }
}
