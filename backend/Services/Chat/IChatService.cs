using backend.Models.Database;

namespace backend.Services;

public interface IChatService
{
    Task<ChatConversation?> CreateConversationAsync(
        Guid userId,
        string? title = null,
        CancellationToken cancellationToken = default);

    Task<ChatConversation?> GetConversationByIdAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<List<ChatConversation>> GetConversationsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ConversationDetailsDto?> GetConversationDetailsAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<List<ChatMessage>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<ChatMessage?> AddMessageAsync(
        Guid conversationId,
        string sender,
        string message,
        decimal? extractedBudget = null,
        string? extractedOrigin = null,
        string? extractedDestination = null,
        Guid? tripSearchId = null,
        DateTime? createdAt = null,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateConversationTitleAsync(
        Guid conversationId,
        string? title,
        CancellationToken cancellationToken = default);

    Task<bool> UpdatePlanningStateAsync(
        Guid conversationId,
        string? planningStateJson,
        CancellationToken cancellationToken = default);
}

public sealed record ConversationDetailsDto(
    Guid ConversationId,
    Guid UserId,
    string? Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ChatMessageDto> Messages);

public sealed record ChatMessageDto(
    Guid MessageId,
    Guid ConversationId,
    string Sender,
    string Message,
    decimal? ExtractedBudget,
    string? ExtractedOrigin,
    string? ExtractedDestination,
    Guid? TripSearchId,
    DateTime CreatedAt);
