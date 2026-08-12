using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class chat_message
{
    public Guid message_id { get; set; }

    public Guid conversation_id { get; set; }

    public string sender { get; set; } = null!;

    public string message { get; set; } = null!;

    public decimal? extracted_budget { get; set; }

    public string? extracted_origin { get; set; }

    public string? extracted_destination { get; set; }

    public Guid? trip_search_id { get; set; }

    public DateTime created_at { get; set; }

    public virtual chat_conversation conversation { get; set; } = null!;

    public virtual trip_search? trip_search { get; set; }
}
