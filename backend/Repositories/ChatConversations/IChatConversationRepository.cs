using backend.Models.Database;

namespace backend.Repositories;

public interface IChatConversationRepository
{
    Task<List<chat_conversation>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<chat_conversation?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<chat_conversation> AddAsync(chat_conversation conversation, CancellationToken cancellationToken = default);

    Task<bool> UpdateTitleAsync(Guid conversationId, string? title, CancellationToken cancellationToken = default);
}
