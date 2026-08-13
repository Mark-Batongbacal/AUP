using backend.Models.Database;

namespace backend.Repositories;

public interface IChatConversationRepository
{
    Task<List<ChatConversation>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ChatConversation?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<ChatConversation> AddAsync(ChatConversation Conversation, CancellationToken cancellationToken = default);

    Task<bool> UpdateTitleAsync(Guid conversationId, string? Title, CancellationToken cancellationToken = default);
}
