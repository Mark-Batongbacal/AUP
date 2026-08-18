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

public sealed class NemotronNavigationSpeechService(IConfiguration configuration)
    : INavigationSpeechService
{
    public async Task<string> PhraseAsync(
        NavigationSpeechContext context,
        CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(
            configuration["Nvidia:ApiKeyEnvironmentVariable"] ?? "NVIDIA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("The configured NVIDIA API key is unavailable.");

        var client = new ChatClient(
            configuration["Nvidia:Model"] ?? "nvidia/nemotron-3-ultra-550b-a55b",
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(configuration["Nvidia:BaseUrl"] ??
                    "https://integrate.api.nvidia.com/v1")
            });
        var response = await client.CompleteChatAsync(
        [
            new SystemChatMessage("""
                You are Tuki, a warm, cheerful toucan commuting companion. Write exactly one
                short, useful navigation sentence in natural Filipino, Taglish, or English.
                Use only facts present in the supplied JSON. Never add a route, landmark,
                direction, distance, event, or claim that is not supplied. Do not expose
                technical state names. Useful first, personality second; emoji is optional.
                Return plain text only.
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
