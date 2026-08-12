using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class chat_conversation
{
    public Guid conversation_id { get; set; }

    public Guid user_id { get; set; }

    public string? title { get; set; }

    public DateTime created_at { get; set; }

    public DateTime updated_at { get; set; }

    public virtual ICollection<chat_message> chat_messages { get; set; } = new List<chat_message>();

    public virtual user_profile user { get; set; } = null!;
}
