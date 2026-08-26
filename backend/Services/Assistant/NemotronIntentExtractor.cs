using System.Diagnostics;
using System.ClientModel;
using System.ClientModel.Primitives;
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
    private readonly ILogger<NemotronIntentExtractor> _logger;
    private readonly string _model;

    public NemotronIntentExtractor(
        IConfiguration configuration,
        ILogger<NemotronIntentExtractor> logger)
    {
        _logger = logger;
        var apiKey = Environment.GetEnvironmentVariable(
            configuration["Qwen:ApiKeyEnvironmentVariable"] ?? "GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "The configured assistant model API key is unavailable.");

        _model = configuration["Qwen:Model"] ?? "gemini-3.5-flash-lite";
        _client = new ChatClient(
            _model,
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(
                    configuration["Qwen:BaseUrl"] ??
                    "https://generativelanguage.googleapis.com/v1beta/openai/"),
                RetryPolicy = new ClientRetryPolicy(0),
                Transport = HttpClientPipelineTransport.Shared
            });
    }

    public async Task<AssistantIntent> ExtractAsync(
        AssistantContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ModelTimeout);
        var started = Stopwatch.GetTimestamp();
        var stage = "ContextReady";

        try
        {
            var contextJson = JsonSerializer.Serialize(context);
            var systemPrompt = Prompt(context.Surface);
            _logger.LogDebug(
                "AI.Intent.ContextReady ElapsedMs={ElapsedMs} Model={Model} Surface={Surface} RecentTurns={RecentTurns} ContextChars={ContextChars}",
                ElapsedMilliseconds(started),
                _model,
                context.Surface,
                context.Conversation.RecentTurns.Count,
                contextJson.Length);

            List<ChatMessage> messages =
            [
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(contextJson)
            ];
            stage = "RequestBuilt";
            _logger.LogDebug(
                "AI.Intent.RequestBuilt ElapsedMs={ElapsedMs} Model={Model} MessageCount={MessageCount} RequestChars={RequestChars} TimeoutSeconds={TimeoutSeconds}",
                ElapsedMilliseconds(started),
                _model,
                messages.Count,
                systemPrompt.Length + contextJson.Length,
                ModelTimeout.TotalSeconds);

            stage = "ApiCall";
            _logger.LogDebug(
                "AI.Intent.ApiCall.Start ElapsedMs={ElapsedMs} Model={Model}",
                ElapsedMilliseconds(started),
                _model);
            var response = await _client.CompleteChatAsync(
                messages,
                cancellationToken: timeout.Token);
            _logger.LogDebug(
                "AI.Intent.ApiCall.Completed ElapsedMs={ElapsedMs} Model={Model}",
                ElapsedMilliseconds(started),
                _model);

            stage = "ResponseParse";
            _logger.LogDebug(
                "AI.Intent.ResponseParse.Start ElapsedMs={ElapsedMs} Model={Model}",
                ElapsedMilliseconds(started),
                _model);
            var json = response.Value.Content.FirstOrDefault()?.Text?.Trim() ?? string.Empty;
            if (json.StartsWith("```", StringComparison.Ordinal))
                json = json.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("```", "", StringComparison.Ordinal)
                    .Trim();

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var actionText = Text(root, "action") ?? Text(root, "intent");
            if (!Enum.TryParse<AssistantIntentType>(actionText, true, out var intentType))
                intentType = AssistantIntentType.Unknown;

            var result = new AssistantIntent
            {
                Intent = intentType,
                DestinationQuery = Text(root, "destinationQuery"),
                OriginQuery = Text(root, "originQuery"),
                BudgetPesos = Decimal(root, "budgetPesos"),
                Preference = NormalizePreference(Text(root, "preference")),
                MaxWalkingMeters = Double(root, "maxWalkingMeters"),
                WalkingPreference = NormalizeWalkingPreference(Text(root, "walkingPreference")),
                AvoidTransportModes = NormalizeAvoidModes(root),
                ResponseType = Text(root, "responseType")
            };
            _logger.LogDebug(
                "AI.Intent.ResponseParse.Completed ElapsedMs={ElapsedMs} Model={Model} ResponseChars={ResponseChars} Intent={Intent}",
                ElapsedMilliseconds(started),
                _model,
                json.Length,
                result.Intent);
            return result;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "AI.Intent.Timeout Stage={Stage} ElapsedMs={ElapsedMs} Model={Model} TimeoutSeconds={TimeoutSeconds} ExtractorTimeoutFired={ExtractorTimeoutFired} CallerCancellationRequested={CallerCancellationRequested} ExceptionChain={ExceptionChain}",
                stage,
                ElapsedMilliseconds(started),
                _model,
                ModelTimeout.TotalSeconds,
                timeout.IsCancellationRequested,
                cancellationToken.IsCancellationRequested,
                ExceptionChain(exception));
            throw new TimeoutException(
                $"Assistant intent extraction exceeded {ModelTimeout.TotalSeconds:0} seconds.",
                exception);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "AI.Intent.Failed Stage={Stage} ElapsedMs={ElapsedMs} Model={Model} HttpStatus={HttpStatus} ExtractorTimeoutFired={ExtractorTimeoutFired} CallerCancellationRequested={CallerCancellationRequested} ExceptionChain={ExceptionChain}",
                stage,
                ElapsedMilliseconds(started),
                _model,
                exception is ClientResultException clientResult ? clientResult.Status : null,
                timeout.IsCancellationRequested,
                cancellationToken.IsCancellationRequested,
                ExceptionChain(exception));
            throw;
        }
    }

    private static double ElapsedMilliseconds(long started) =>
        Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private static string ExceptionChain(Exception exception)
    {
        var names = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
            names.Add(current.GetType().Name);
        return string.Join(" -> ", names);
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
                - Use UpdateTripConstraints for explicit remaining-trip changes such as a walking preference, avoiding a transport mode, or a changed optimization preference.
                - Use ChangeDestination only when the passenger explicitly says they want to change the active trip destination.
                - A place mention by itself is NOT a destination change.
                - Preference changes on this surface are intended to reroute the remaining active trip automatically after the backend validates the result.
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

        var walkingRules = surface == AssistantSurface.ActiveTrip
            ? """
                ACTIVE-TRIP WALKING PREFERENCE RULES:
                - Walking preference is a hard maximum for the remaining rerouted journey.
                - LESS / "less walking" / "I don't like walking" / "pagod ako" => walkingPreference=LESS and maxWalkingMeters=1800.
                - NORMAL / neutral or normal walking preference => walkingPreference=NORMAL and maxWalkingMeters=2150.
                - MORE / "I prefer walking" / "I don't mind walking farther" / "okay lang maglakad" => walkingPreference=MORE and maxWalkingMeters=2500.
                - If the passenger gives an explicit walking-distance limit, use that exact non-negative number instead of 1800/2150/2500.
                """
            : """
                PLANNING WALKING PREFERENCE RULES:
                - A numeric walking distance is a hard maxWalkingMeters.
                - "Okay lang maglakad" and "I don't mind walking farther" mean walkingPreference=MORE with maxWalkingMeters=null.
                - "Pagod ako" and "I don't want to walk much" mean walkingPreference=LESS with maxWalkingMeters=null.
                - Do not invent an exact maxWalkingMeters from a vague walking phrase on the planning surface.
                """;

        return $$"""
            You are the intent interpreter for Tuki, a Philippine commute assistant.
            Return exactly ONE JSON object and no prose. The backend owns routing, GPS, fares, turns, boarding/alighting, persistence, and rerouting. You only interpret the passenger's language.

            {{surfaceRules}}

            {{walkingRules}}

            GENERAL RULES:
            - Do not calculate routes, fares, ETA, coordinates, or distances.
            - A numeric budget is a hard maximum only when the passenger explicitly gives a money amount. "I'm kinda broke" means preference=cheapest and budgetPesos=null.
            - Avoid modes may only contain WALK, TRICYCLE, or JEEPNEY.
            - "trike", "tricycle", and "TODA" avoidance normalize to TRICYCLE. "jeep" and "jeepney" avoidance normalize to JEEPNEY.
            - Vehicle avoidance is a hard constraint. If the passenger asks to avoid a mode, include it in avoidTransportModes; do not weaken it because another route seems cheaper or faster.
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
              "walkingPreference":"LESS|NORMAL|MORE"|null,
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

    private static AssistantWalkingPreference? NormalizeWalkingPreference(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "LESS" => AssistantWalkingPreference.Less,
            "NORMAL" => AssistantWalkingPreference.Normal,
            "MORE" => AssistantWalkingPreference.More,
            _ => null
        };

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
