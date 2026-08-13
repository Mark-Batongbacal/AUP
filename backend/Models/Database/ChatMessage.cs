using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class ChatMessage
{
    public Guid MessageId { get; set; }

    public Guid ConversationId { get; set; }

    public string Sender { get; set; } = null!;

    public string Message { get; set; } = null!;

    public decimal? ExtractedBudget { get; set; }

    public string? ExtractedOrigin { get; set; }

    public string? ExtractedDestination { get; set; }

    public Guid? TripSearchId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ChatConversation Conversation { get; set; } = null!;

    public virtual TripSearch? TripSearch { get; set; }
}
