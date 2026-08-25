using System.Text.Json;
using OpenAI;
using OpenAI.Chat;

namespace backend.Services.Assistant;

// Kept under the existing class name so current DI wiring remains compatible.
// The configured provider/model is selected entirely through configuration.
public sealed class NemotronIntentExtractor : IAssistantIntentExtractor
{
    private static readonly TimeSpan ModelTimeout = TimeSpan.FromSeconds(15);
    private readonly ChatClient _client;

    public NemotronIntentExtractor(IConfiguration configuration)
    {
        var apiKey = Environment.GetEnvironmentVariable(
            configuration["Qwen:ApiKeyEnvironmentVariable"] ?? "GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "The configured assistant model API key is unavailable.");

        _client = new ChatClient(
            configuration["Qwen:Model"] ?? "gemini-3.5-flash-lite",
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(
                    configuration["Qwen:BaseUrl"] ??
                    "https://generativelanguage.googleapis.com/v1beta/openai/")
            });
    }

    public async Task<AssistantIntent> ExtractAsync(
        AssistantContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ModelTimeout);

        try
        {
            var response = await _client.CompleteChatAsync(
            [
                new SystemChatMessage(Prompt(context.Surface)),
                new UserChatMessage(JsonSerializer.Serialize(context))
            ], cancellationToken: timeout.Token);

            var json = response.Value.Content.FirstOrDefault()?.Text?.Trim() ?? string.Empty;
            if (json.StartsWith("```", StringComparison.Ordinal))
                json = json.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("```", "", StringComparison.Ordinal)
                    .Trim();

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var actionText = Text(root, "action") ?? Text(root, "intent");
            if (!Enum.TryParse<AssistantIntentType>(actionText, true, out var intent))
                intent = AssistantIntentType.Unknown;

            return new AssistantIntent
            {
                Intent = intent,
                DestinationQuery = Text(root, "destinationQuery"),
                OriginQuery = Text(root, "originQuery"),
                BudgetPesos = Decimal(root, "budgetPesos"),
                Preference = NormalizePreference(Text(root, "preference")),
                MaxWalkingMeters = Double(root, "maxWalkingMeters"),
                AvoidTransportModes = NormalizeAvoidModes(root),
                ResponseType = Text(root, "responseType")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Assistant intent extraction exceeded {ModelTimeout.TotalSeconds:0} seconds.");
        }
    }

    private static string Prompt(AssistantSurface surface)
    {
        var surfaceRules = surface == AssistantSurface.ActiveTrip
            ? """
                ACTIVE-TRIP SURFACE RULES:
                - The supplied ActiveTrip object is authoritative. Never invent trip state, route, fare, distance, stop, location, or database IDs.
                - Use NavigationQuestion for questions about the current/next instruction, stop, route correctness, remaining fare/distance, or current trip state.
                - Use Lost when the passenger says they are lost, went the wrong way, missed a stop, or asks whether they have gone past their stop.
                - Use ExplainRoute when the passenger asks why this route or recommendation was chosen.
                - Use UpdateTripConstraints for explicit remaining-trip changes such as a new budget, less walking, avoiding a transport mode, or a changed optimization preference.
                - Use ChangeDestination only when the passenger explicitly says they want to change the active trip destination.
                - A place mention by itself is NOT a destination change.
                - Never silently approve or commit a route change. Constraint/destination changes are proposals that require backend confirmation.
                - Use CancelTrip when the passenger explicitly asks to end/cancel the active trip.
                """
            : """
                PLANNING SURFACE RULES:
                - This surface plans or fine-tunes a prospective passenger trip; it must not mutate an active trip.
                - Use PlanRoute when the passenger asks for a trip/route, including requests containing budget, walking, mode-avoidance, or fastest/cheapest/efficient preferences.
                - Use UpdateTripConstraints for follow-up tuning such as "cheaper", "less walking", or "no tricycle" when the conversation already contains a destination.
                - Use SearchPlace when the passenger is only looking for a place and has not asked for a route.
                - Use ChangeDestination when the passenger explicitly replaces the destination of the prospective plan.
                - Use GeneralChat for harmless small talk that does not require routing.
                """;

        return $$"""
            You are the intent interpreter for Tuki, a Philippine commute assistant.
            Return exactly ONE JSON object and no prose. The backend owns routing, GPS, fares, turns, boarding/alighting, persistence, and rerouting. You only interpret the passenger's language.

            {{surfaceRules}}

            GENERAL RULES:
            - Do not calculate routes, fares, ETA, coordinates, or distances.
            - Do not invent missing values. Vague phrases such as "pagod ako" may imply a soft less-walking preference, but must NOT invent an exact maxWalkingMeters.
            - A numeric budget is a hard maximum only when the passenger explicitly gives a money amount.
            - Avoid modes may only contain WALK, TRICYCLE, or JEEPNEY.
            - preference may only be fastest, cheapest, or efficient.
            - If the current message depends on prior conversation, use the supplied Conversation.RecentTurns and LastDestinationQuery rather than guessing.
            - Ask no question yourself; return the action and extracted values and let the deterministic backend decide whether clarification is needed.

            Schema:
            {
              "action":"PlanRoute|SearchPlace|UpdateTripConstraints|ChangeDestination|ExplainRoute|StartNavigation|NavigationQuestion|Lost|CancelTrip|ConfirmAction|RejectAction|GeneralChat|Unknown",
              "destinationQuery":string|null,
              "originQuery":string|null,
              "budgetPesos":number|null,
              "preference":"fastest|cheapest|efficient"|null,
              "maxWalkingMeters":number|null,
              "avoidTransportModes":["WALK"|"TRICYCLE"|"JEEPNEY"],
              "responseType":string|null
            }
            """;
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;

    private static decimal? Decimal(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDecimal(out var number) &&
        number >= 0
            ? number
            : null;

    private static double? Double(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number) &&
        double.IsFinite(number) &&
        number >= 0
            ? number
            : null;

    private static string? NormalizePreference(string? value) =>
        value?.ToLowerInvariant() is "fastest" or "cheapest" or "efficient"
            ? value.ToLowerInvariant()
            : null;

    private static List<string> NormalizeAvoidModes(JsonElement root)
    {
        if (!root.TryGetProperty("avoidTransportModes", out var value) ||
            value.ValueKind != JsonValueKind.Array)
            return [];

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => NormalizeMode(item.GetString()))
            .Where(item => item is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeMode(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "TRIKE" or "TRICYCLE" or "TODA" => "TRICYCLE",
            "WALK" or "WALKING" or "PEDESTRIAN" => "WALK",
            "JEEP" or "JEEPNEY" => "JEEPNEY",
            _ => null
        };
}
