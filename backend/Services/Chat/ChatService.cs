using backend.Models.Database;
using backend.Repositories;

namespace backend.Services;

public sealed class ChatService(
    IChatConversationRepository conversationRepository,
    IChatMessageRepository messageRepository,
    ITripSearchRepository tripSearchRepository) : IChatService
{
    private const int MaxTitleLength = 200;
    private const int MaxSenderLength = 20;

    private readonly IChatConversationRepository _conversationRepository = conversationRepository;
    private readonly IChatMessageRepository _messageRepository = messageRepository;
    private readonly ITripSearchRepository _tripSearchRepository = tripSearchRepository;

    public async Task<ChatConversation?> CreateConversationAsync(
        Guid userId,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedTitle = NormalizeOptionalText(title);
        if (userId == Guid.Empty || IsTooLong(normalizedTitle, MaxTitleLength))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var conversation = new ChatConversation
        {
            UserId = userId,
            Title = normalizedTitle,
            CreatedAt = now,
            UpdatedAt = now,
        };

        return await _conversationRepository.AddAsync(conversation, cancellationToken);
    }

    public Task<ChatConversation?> GetConversationByIdAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (conversationId == Guid.Empty)
        {
            return Task.FromResult<ChatConversation?>(null);
        }

        return _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
    }

    public Task<List<ChatConversation>> GetConversationsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult(new List<ChatConversation>());
        }

        return _conversationRepository.GetByUserAsync(userId, cancellationToken);
    }

    public async Task<ConversationDetailsDto?> GetConversationDetailsAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (conversationId == Guid.Empty)
        {
            return null;
        }

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        // Message repository owns chronological ordering by CreatedAt.
        var messages = await _messageRepository.GetByConversationAsync(conversationId, cancellationToken);

        return MapConversationDetails(conversation, messages);
    }

    public Task<List<ChatMessage>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (conversationId == Guid.Empty)
        {
            return Task.FromResult(new List<ChatMessage>());
        }

        return _messageRepository.GetByConversationAsync(conversationId, cancellationToken);
    }

    public async Task<ChatMessage?> AddMessageAsync(
        Guid conversationId,
        string sender,
        string message,
        decimal? extractedBudget = null,
        string? extractedOrigin = null,
        string? extractedDestination = null,
        Guid? tripSearchId = null,
        DateTime? createdAt = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSender = NormalizeRequiredText(sender);
        var normalizedMessage = NormalizeRequiredText(message);
        if (conversationId == Guid.Empty ||
            normalizedSender is null ||
            normalizedMessage is null ||
            IsTooLong(normalizedSender, MaxSenderLength) ||
            extractedBudget < 0 ||
            tripSearchId == Guid.Empty)
        {
            return null;
        }

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        if (tripSearchId.HasValue && !await TripSearchExistsAsync(tripSearchId.Value, cancellationToken))
        {
            return null;
        }

        var chatMessage = new ChatMessage
        {
            ConversationId = conversationId,
            Sender = normalizedSender,
            Message = normalizedMessage,
            ExtractedBudget = extractedBudget,
            ExtractedOrigin = NormalizeOptionalText(extractedOrigin),
            ExtractedDestination = NormalizeOptionalText(extractedDestination),
            TripSearchId = tripSearchId,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };

        var createdMessage = await _messageRepository.AddAsync(chatMessage, cancellationToken);

        // Keep conversation recency aligned with new messages using the existing metadata update path.
        await _conversationRepository.UpdateTitleAsync(conversationId, conversation.Title, cancellationToken);

        return createdMessage;
    }

    public Task<bool> UpdateConversationTitleAsync(
        Guid conversationId,
        string? title,
        CancellationToken cancellationToken = default)
    {
        var normalizedTitle = NormalizeOptionalText(title);
        if (conversationId == Guid.Empty || IsTooLong(normalizedTitle, MaxTitleLength))
        {
            return Task.FromResult(false);
        }

        return _conversationRepository.UpdateTitleAsync(conversationId, normalizedTitle, cancellationToken);
    }

    public Task<bool> UpdatePlanningStateAsync(
        Guid conversationId,
        string? planningStateJson,
        CancellationToken cancellationToken = default) =>
        conversationId == Guid.Empty
            ? Task.FromResult(false)
            : _conversationRepository.UpdatePlanningStateAsync(
                conversationId, planningStateJson, cancellationToken);

    private async Task<bool> TripSearchExistsAsync(Guid tripSearchId, CancellationToken cancellationToken)
    {
        var tripSearch = await _tripSearchRepository.GetByIdAsync(tripSearchId, cancellationToken);
        return tripSearch is not null;
    }

    private static ConversationDetailsDto MapConversationDetails(
        ChatConversation conversation,
        IReadOnlyList<ChatMessage> messages) =>
        new(
            conversation.ConversationId,
            conversation.UserId,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            messages.Select(MapChatMessage).ToList());

    private static ChatMessageDto MapChatMessage(ChatMessage message) =>
        new(
            message.MessageId,
            message.ConversationId,
            message.Sender,
            message.Message,
            message.ExtractedBudget,
            message.ExtractedOrigin,
            message.ExtractedDestination,
            message.TripSearchId,
            message.CreatedAt);

    private static bool IsTooLong(string? value, int maxLength) =>
        value is not null && value.Length > maxLength;

    private static string? NormalizeRequiredText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string? NormalizeOptionalText(string? value) =>
        NormalizeRequiredText(value);
}
