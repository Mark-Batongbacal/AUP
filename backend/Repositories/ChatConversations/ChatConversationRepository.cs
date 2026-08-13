using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

/// <summary>
/// Data access for chat conversations. AI Request handling belongs outside repositories.
/// </summary>
public sealed class ChatConversationRepository(SupabaseDbContext context) : IChatConversationRepository
{
    private readonly SupabaseDbContext _context = context;

    public Task<List<ChatConversation>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.ChatConversations
            .AsNoTracking()
            .Where(Conversation => Conversation.UserId == userId)
            .OrderByDescending(Conversation => Conversation.UpdatedAt)
            .ToListAsync(cancellationToken);

    public Task<ChatConversation?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        _context.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(Conversation => Conversation.ConversationId == conversationId, cancellationToken);

    public async Task<ChatConversation> AddAsync(ChatConversation Conversation, CancellationToken cancellationToken = default)
    {
        await _context.ChatConversations.AddAsync(Conversation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Conversation;
    }

    public async Task<bool> UpdateTitleAsync(Guid conversationId, string? Title, CancellationToken cancellationToken = default)
    {
        var Conversation = await _context.ChatConversations.FirstOrDefaultAsync(
            Conversation => Conversation.ConversationId == conversationId,
            cancellationToken);

        if (Conversation is null)
        {
            return false;
        }

        Conversation.Title = Title;
        Conversation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
