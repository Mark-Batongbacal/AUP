using backend.Models.Database;

namespace backend.Repositories;

public interface IChatMessageRepository
{
    Task<List<chat_message>> GetByConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<chat_message> AddAsync(chat_message message, CancellationToken cancellationToken = default);

    Task<chat_message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default);
}
