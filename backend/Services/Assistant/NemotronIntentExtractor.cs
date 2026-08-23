using System.Text.Json;
using OpenAI;
using OpenAI.Chat;

namespace backend.Services.Assistant;

public sealed class NemotronIntentExtractor : IAssistantIntentExtractor
{
    private static readonly TimeSpan QwenTimeout = TimeSpan.FromSeconds(15);
    private readonly ChatClient _client;

    public NemotronIntentExtractor(IConfiguration configuration)
    {
        var apiKey = Environment.GetEnvironmentVariable(
            configuration["Qwen:ApiKeyEnvironmentVariable"] ?? "OPENROUTER_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "The configured Qwen API key is unavailable.");

        _client = new ChatClient(
            configuration["Qwen:Model"] ?? "qwen/qwen3-14b:free",
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(
                    configuration["Qwen:BaseUrl"] ??
                    "https://openrouter.ai/api/v1")
            });
    }

    public async Task<AssistantIntent> ExtractAsync(
        string message, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(QwenTimeout);

        try
        {
            var response = await _client.CompleteChatAsync(
            [
                new SystemChatMessage("""
                    Return one JSON object only. Extract conversational intent; never calculate a route.
                    Schema: {"intent":"PlanRoute|ClarifyDestination|StartNavigation|NavigationQuestion|Lost|CancelTrip|Unknown","destinationQuery":string|null,"originQuery":string|null,"budgetPesos":number|null,"preference":"fastest|cheapest|efficient"|null,"tripSessionId":string|null}.
                    Lost includes statements or questions about being lost, missing a stop, current location, or whether travel is still correct.
                    Do not invent missing values.
                    """),
                new UserChatMessage(message)
            ], cancellationToken: timeout.Token);
            var json = response.Value.Content.FirstOrDefault()?.Text?.Trim() ?? "";
            if (json.StartsWith("```"))
                json = json.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("```", "").Trim();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("intent", out var intentValue) ||
                !Enum.TryParse<AssistantIntentType>(intentValue.GetString(), true, out var intent))
                intent = AssistantIntentType.Unknown;
            return new AssistantIntent
            {
                Intent = intent,
                DestinationQuery = Text(root, "destinationQuery"),
                OriginQuery = Text(root, "originQuery"),
                BudgetPesos = Decimal(root, "budgetPesos"),
                Preference = NormalizePreference(Text(root, "preference")),
                TripSessionId = Guid.TryParse(Text(root, "tripSessionId"), out var id) ? id : null
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Qwen intent extraction exceeded {QwenTimeout.TotalSeconds:0} seconds.");
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() : null;
    private static decimal? Decimal(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out var number) && number >= 0
            ? number : null;
    private static string? NormalizePreference(string? value) =>
        value?.ToLowerInvariant() is "fastest" or "cheapest" or "efficient"
            ? value.ToLowerInvariant() : null;
}
