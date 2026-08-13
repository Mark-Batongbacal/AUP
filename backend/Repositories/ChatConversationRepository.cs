using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for chat conversations. AI request handling belongs outside repositories.
/// </summary>
public sealed class ChatConversationRepository(SupabaseDbContext context) : IChatConversationRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<chat_conversation>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.chat_conversations
            .AsNoTracking()
            .Where(conversation => conversation.user_id == userId)
            .OrderByDescending(conversation => conversation.updated_at)
            .ToListAsync(cancellationToken);

    public Task<chat_conversation?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _context.chat_conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(conversation => conversation.conversation_id == conversationId, cancellationToken);

    public async Task<chat_conversation> AddAsync(chat_conversation conversation, CancellationToken cancellationToken = default)
    {
        await _context.chat_conversations.AddAsync(conversation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    public async Task<bool> UpdateTitleAsync(Guid conversationId, string? title, CancellationToken cancellationToken = default)
    {
        var conversation = await _context.chat_conversations.FirstOrDefaultAsync(
            conversation => conversation.conversation_id == conversationId,
            cancellationToken);

        if (conversation is null)
        {
            return false;
        }

        conversation.title = title;
        conversation.updated_at = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
