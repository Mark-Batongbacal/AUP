using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for chat messages. Message retrieval for a Conversation is ordered by CreatedAt.
/// </summary>
public sealed class ChatMessageRepository(SupabaseDbContext context) : IChatMessageRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<ChatMessage>> GetByConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _context.ChatMessages
            .AsNoTracking()
            .Where(Message => Message.ConversationId == conversationId)
            .OrderBy(Message => Message.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<ChatMessage> AddAsync(ChatMessage Message, CancellationToken cancellationToken = default)
    {
        await _context.ChatMessages.AddAsync(Message, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Message;
    }

    public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        _context.ChatMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(Message => Message.MessageId == messageId, cancellationToken);
}
