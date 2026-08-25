/*
   Conversation-scoped assistant planning state. This is intentionally kept
   off UserProfiles: destination resolution and routing constraints belong to
   one planning conversation, not to a user's durable defaults.
*/
IF COL_LENGTH(N'dbo.ChatConversations', N'PlanningStateJson') IS NULL
BEGIN
    ALTER TABLE dbo.ChatConversations
        ADD PlanningStateJson nvarchar(max) NULL;
END;
