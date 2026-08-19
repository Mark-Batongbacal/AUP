SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.UserProfiles', N'IsEmailVerified') IS NULL
BEGIN
    ALTER TABLE dbo.UserProfiles
        ADD IsEmailVerified BIT NOT NULL CONSTRAINT DF_UserProfiles_IsEmailVerified DEFAULT (0);
END;

IF OBJECT_ID(N'dbo.EmailVerificationTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailVerificationTokens
    (
        EmailVerificationTokenId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_EmailVerificationTokens_Id DEFAULT NEWSEQUENTIALID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        TokenHash NVARCHAR(128) NOT NULL,
        ExpiresAt DATETIME2 NOT NULL,
        ConsumedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_EmailVerificationTokens_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_EmailVerificationTokens PRIMARY KEY (EmailVerificationTokenId),
        CONSTRAINT FK_EmailVerificationTokens_UserProfiles
            FOREIGN KEY (UserId) REFERENCES dbo.UserProfiles(UserId) ON DELETE CASCADE
    );

    CREATE INDEX IX_EmailVerificationTokens_User ON dbo.EmailVerificationTokens (UserId);
    CREATE UNIQUE INDEX UX_EmailVerificationTokens_TokenHash ON dbo.EmailVerificationTokens (TokenHash);
END;

IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        PasswordResetTokenId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PasswordResetTokens_Id DEFAULT NEWSEQUENTIALID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        TokenHash NVARCHAR(128) NOT NULL,
        ExpiresAt DATETIME2 NOT NULL,
        ConsumedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PasswordResetTokens PRIMARY KEY (PasswordResetTokenId),
        CONSTRAINT FK_PasswordResetTokens_UserProfiles
            FOREIGN KEY (UserId) REFERENCES dbo.UserProfiles(UserId) ON DELETE CASCADE
    );

    CREATE INDEX IX_PasswordResetTokens_User ON dbo.PasswordResetTokens (UserId);
    CREATE UNIQUE INDEX UX_PasswordResetTokens_TokenHash ON dbo.PasswordResetTokens (TokenHash);
END;

COMMIT TRANSACTION;
