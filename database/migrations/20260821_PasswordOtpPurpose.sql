SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        PasswordResetTokenId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PasswordResetTokens_Id DEFAULT NEWSEQUENTIALID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        TokenHash NVARCHAR(128) NOT NULL,
        Purpose NVARCHAR(20) NOT NULL CONSTRAINT DF_PasswordResetTokens_Purpose DEFAULT (N'Reset'),
        ExpiresAt DATETIME2 NOT NULL,
        ConsumedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PasswordResetTokens PRIMARY KEY (PasswordResetTokenId),
        CONSTRAINT FK_PasswordResetTokens_UserProfiles
            FOREIGN KEY (UserId) REFERENCES dbo.UserProfiles(UserId) ON DELETE CASCADE
    );

    CREATE INDEX IX_PasswordResetTokens_User ON dbo.PasswordResetTokens (UserId);
    CREATE UNIQUE INDEX UX_PasswordResetTokens_TokenHash ON dbo.PasswordResetTokens (TokenHash);
END
ELSE IF COL_LENGTH(N'dbo.PasswordResetTokens', N'Purpose') IS NULL
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
