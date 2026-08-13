using backend.Models.Database;

namespace backend.Repositories;

public interface IChatMessageRepository
{
    Task<List<ChatMessage>> GetByConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<ChatMessage> AddAsync(ChatMessage Message, CancellationToken cancellationToken = default);

    Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default);
}
