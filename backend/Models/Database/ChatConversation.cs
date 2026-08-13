using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class ChatConversation
{
    public Guid ConversationId { get; set; }

    public Guid UserId { get; set; }

    public string? Title { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual UserProfile User { get; set; } = null!;
}
