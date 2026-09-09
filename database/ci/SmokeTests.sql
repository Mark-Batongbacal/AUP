SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Basic connectivity and expected schema checks.
IF DB_NAME() <> N'TukiCi'
    THROW 51000, 'CI is connected to the wrong database.', 1;

IF OBJECT_ID(N'dbo.UserProfiles', N'U') IS NULL
    THROW 51001, 'dbo.UserProfiles is missing.', 1;

IF OBJECT_ID(N'dbo.TransportRoutes', N'U') IS NULL
    THROW 51002, 'dbo.TransportRoutes is missing.', 1;

IF OBJECT_ID(N'dbo.TripSessions', N'U') IS NULL
    THROW 51003, 'dbo.TripSessions is missing.', 1;

IF OBJECT_ID(N'dbo.LocalUserCredentials', N'U') IS NULL
    THROW 51004, 'dbo.LocalUserCredentials is missing.', 1;

IF OBJECT_ID(N'dbo.FavoriteTrips', N'U') IS NULL
    THROW 51005, 'dbo.FavoriteTrips is missing.', 1;

IF OBJECT_ID(N'dbo.EmailVerificationTokens', N'U') IS NULL
    THROW 51006, 'dbo.EmailVerificationTokens is missing.', 1;

IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
    THROW 51007, 'dbo.PasswordResetTokens is missing.', 1;

IF OBJECT_ID(N'dbo.ApiKeySessions', N'U') IS NULL
    THROW 51008, 'dbo.ApiKeySessions is missing.', 1;

IF COL_LENGTH(N'dbo.TransportRoutes', N'ArchivedAt') IS NULL
    THROW 51009, 'TransportRoutes.ArchivedAt migration was not applied.', 1;

IF COL_LENGTH(N'dbo.UserProfiles', N'IsEmailVerified') IS NULL
    THROW 51010, 'UserProfiles.IsEmailVerified migration was not applied.', 1;

IF COL_LENGTH(N'dbo.TripSessions', N'LastSpeechEventKey') IS NULL
    THROW 51011, 'TripSessions.LastSpeechEventKey migration was not applied.', 1;

-- Stable seed data is part of the schema contract.
IF NOT EXISTS (SELECT 1 FROM dbo.TransportModes WHERE ModeCode = N'WALK')
    THROW 51012, 'WALK transport mode seed is missing.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.TransportModes WHERE ModeCode = N'JEEPNEY')
    THROW 51013, 'JEEPNEY transport mode seed is missing.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.TransportModes WHERE ModeCode = N'TRICYCLE')
    THROW 51014, 'TRICYCLE transport mode seed is missing.', 1;

-- Real read/write behavior test. Everything is rolled back so the schema test
-- remains deterministic and does not leave fixture data behind.
BEGIN TRANSACTION;

DECLARE @UserId uniqueidentifier = NEWID();
DECLARE @Email nvarchar(255) = CONCAT(N'ci-', CONVERT(nvarchar(36), @UserId), N'@example.invalid');

INSERT INTO dbo.UserProfiles (UserId, Email)
VALUES (@UserId, @Email);

IF NOT EXISTS (
    SELECT 1
    FROM dbo.UserProfiles
    WHERE UserId = @UserId
      AND Email = @Email
      AND IsActive = 1)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51015, 'UserProfiles insert/read smoke test failed.', 1;
END;

ROLLBACK TRANSACTION;

SELECT
    N'PASS' AS Result,
    DB_NAME() AS DatabaseName,
    (SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo')) AS DboTableCount;
