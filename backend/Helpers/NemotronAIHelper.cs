using OpenAI;
using OpenAI.Chat;

namespace backend.Helpers;

public class NemotronAIHelper
{
    private readonly ChatClient _client;

    public NemotronAIHelper(IConfiguration configuration)
    {
        var apiKey = configuration["NVIDIA_API_KEY"];

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("NVIDIA_API_KEY is not configured.");

        _client = new ChatClient(
            model: "nvidia/nemotron-3-ultra-550b-a55b",
            credential: new System.ClientModel.ApiKeyCredential(apiKey),
            options: new OpenAIClientOptions
            {
                Endpoint = new Uri("https://integrate.api.nvidia.com/v1")
            }
        );
    }

    public async Task<string> AskAsync(string message)
    {
        var response = await _client.CompleteChatAsync(
        [
            new UserChatMessage(message)
        ]);

        return response.Value.Content[0].Text;
    }
}