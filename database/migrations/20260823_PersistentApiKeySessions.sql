SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ApiKeySessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApiKeySessions
    (
        ApiKeySessionId BIGINT IDENTITY(1, 1) NOT NULL,
        KeyHash CHAR(64) NOT NULL,
        CredentialOwner NVARCHAR(255) NOT NULL,
        CreatedAt DATETIMEOFFSET(7) NOT NULL
            CONSTRAINT DF_ApiKeySessions_CreatedAt
            DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
        ExpiresAt DATETIMEOFFSET(7) NOT NULL,
        RevokedAt DATETIMEOFFSET(7) NULL,
        CONSTRAINT PK_ApiKeySessions PRIMARY KEY (ApiKeySessionId)
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ApiKeySessions')
      AND name = N'UX_ApiKeySessions_KeyHash')
BEGIN
    CREATE UNIQUE INDEX UX_ApiKeySessions_KeyHash
        ON dbo.ApiKeySessions (KeyHash);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ApiKeySessions')
      AND name = N'IX_ApiKeySessions_OwnerExpiresAt')
BEGIN
    CREATE INDEX IX_ApiKeySessions_OwnerExpiresAt
        ON dbo.ApiKeySessions (CredentialOwner, ExpiresAt);
END;

COMMIT TRANSACTION;
