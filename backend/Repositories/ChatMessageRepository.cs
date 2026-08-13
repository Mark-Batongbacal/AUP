using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for chat messages. Message retrieval for a conversation is ordered by created_at.
/// </summary>
public sealed class ChatMessageRepository(SupabaseDbContext context)
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<chat_message>> GetByConversationAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _context.chat_messages
            .AsNoTracking()
            .Where(message => message.conversation_id == conversationId)
            .OrderBy(message => message.created_at)
            .ToListAsync(cancellationToken);

    public async Task<chat_message> AddAsync(chat_message message, CancellationToken cancellationToken = default)
    {
        await _context.chat_messages.AddAsync(message, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return message;
    }

    public Task<chat_message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        _context.chat_messages
            .AsNoTracking()
            .FirstOrDefaultAsync(message => message.message_id == messageId, cancellationToken);
}
