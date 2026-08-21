SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.PasswordResetTokens', N'Purpose') IS NULL
BEGIN
    ALTER TABLE dbo.PasswordResetTokens
        ADD Purpose NVARCHAR(20) NOT NULL
            CONSTRAINT DF_PasswordResetTokens_Purpose DEFAULT (N'Reset');
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_PasswordResetTokens_UserPurpose'
      AND object_id = OBJECT_ID(N'dbo.PasswordResetTokens'))
BEGIN
    CREATE INDEX IX_PasswordResetTokens_UserPurpose
        ON dbo.PasswordResetTokens (UserId, Purpose, ExpiresAt)
        INCLUDE (ConsumedAt, TokenHash);
END;

COMMIT TRANSACTION;
