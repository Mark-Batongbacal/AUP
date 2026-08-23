using System.Net.Http.Json;
using System.Text.Json;

namespace backend.Services.AI;

public static class GeminiTextGeneration
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public static bool IsConfigured(IConfiguration? configuration)
    {
        if (configuration is null) return false;
        return !string.IsNullOrWhiteSpace(ReadApiKey(configuration));
    }

    public static async Task<string> GenerateStructuredTextAsync(
        IConfiguration configuration,
        string systemInstruction,
        string userContent,
        string outputProperty,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputProperty))
            throw new ArgumentException("An output property is required.", nameof(outputProperty));

        var apiKey = ReadApiKey(configuration);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("The configured Gemini API key is unavailable.");

        var baseUrl = (configuration["Gemini:BaseUrl"] ??
            "https://generativelanguage.googleapis.com/v1beta/").TrimEnd('/') + "/";
        var model = configuration["Gemini:Model"] ?? "gemini-3.5-flash-lite";
        var endpoint = new Uri(
            new Uri(baseUrl, UriKind.Absolute),
            $"models/{Uri.EscapeDataString(model)}:generateContent");

        var responseSchema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                [outputProperty] = new Dictionary<string, object?>
                {
                    ["type"] = "string"
                }
            },
            ["required"] = new[] { outputProperty },
            ["additionalProperties"] = false
        };

        var requestBody = new Dictionary<string, object?>
        {
            ["system_instruction"] = new
            {
                parts = new[] { new { text = systemInstruction } }
            },
            ["contents"] = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userContent } }
                }
            },
            ["generationConfig"] = new
            {
                maxOutputTokens = 256,
                responseFormat = new
                {
                    text = new
                    {
                        mimeType = "application/json",
                        schema = responseSchema
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

        using var response = await Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Gemini API returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var responseDocument = JsonDocument.Parse(responseJson);
        var text = FirstTextPart(responseDocument.RootElement);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini returned no text content.");

        using var structured = JsonDocument.Parse(text);
        if (!structured.RootElement.TryGetProperty(outputProperty, out var output) ||
            output.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException(
                $"Gemini structured output did not contain '{outputProperty}'.");

        var value = output.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Gemini structured output was empty.");
        return value;
    }

    private static string? ReadApiKey(IConfiguration configuration)
    {
        var environmentVariable = configuration["Gemini:ApiKeyEnvironmentVariable"] ?? "GEMINI_API_KEY";
        return Environment.GetEnvironmentVariable(environmentVariable);
    }

    private static string? FirstTextPart(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    return text.GetString();
            }
        }
        return null;
    }
}
