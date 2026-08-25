namespace backend.Services.Assistant;

public interface IAssistantIntentExtractor
{
    Task<AssistantIntent> ExtractAsync(
        AssistantContext context,
        CancellationToken cancellationToken = default);
}
