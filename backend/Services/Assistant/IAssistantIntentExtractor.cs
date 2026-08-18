namespace backend.Services.Assistant;

public interface IAssistantIntentExtractor
{
    Task<AssistantIntent> ExtractAsync(string message, CancellationToken cancellationToken = default);
}
