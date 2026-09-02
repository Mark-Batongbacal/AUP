SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TripSessions', N'U') IS NULL
        THROW 50001, 'dbo.TripSessions does not exist. Run TukiNavigationSchema.sql first.', 1;

    IF OBJECT_ID(N'dbo.TripLandmarkCandidates', N'U') IS NULL
        THROW 50002, 'dbo.TripLandmarkCandidates does not exist. Run TukiNavigationSchema.sql first.', 1;

    IF COL_LENGTH(N'dbo.TripSessions', N'LastSpeechEventKey') IS NULL
        ALTER TABLE dbo.TripSessions
            ADD LastSpeechEventKey nvarchar(250) NULL;

    IF COL_LENGTH(N'dbo.TripSessions', N'LastSpokenInstruction') IS NULL
        ALTER TABLE dbo.TripSessions
            ADD LastSpokenInstruction nvarchar(500) NULL;

    IF COL_LENGTH(N'dbo.TripLandmarkCandidates', N'Role') IS NULL
        ALTER TABLE dbo.TripLandmarkCandidates
            ADD [Role] nvarchar(30) NOT NULL
                CONSTRAINT DF_TripLandmarkCandidates_Role_20260819
                DEFAULT ('ProgressReference');

    IF COL_LENGTH(N'dbo.TripLandmarkCandidates', N'Relation') IS NULL
        ALTER TABLE dbo.TripLandmarkCandidates
            ADD Relation nvarchar(30) NOT NULL
                CONSTRAINT DF_TripLandmarkCandidates_Relation_20260819
                DEFAULT ('AlongRoute');

    IF COL_LENGTH(N'dbo.TripLandmarkCandidates', N'DistanceFromTargetMeters') IS NULL
        ALTER TABLE dbo.TripLandmarkCandidates
            ADD DistanceFromTargetMeters float NOT NULL
                CONSTRAINT DF_TripLandmarkCandidates_TargetDistance_20260819
                DEFAULT (0);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    COL_LENGTH(N'dbo.TripSessions', N'LastSpeechEventKey') AS LastSpeechEventKeyBytes,
    COL_LENGTH(N'dbo.TripSessions', N'LastSpokenInstruction') AS LastSpokenInstructionBytes,
    COL_LENGTH(N'dbo.TripLandmarkCandidates', N'Role') AS LandmarkRoleBytes,
    COL_LENGTH(N'dbo.TripLandmarkCandidates', N'Relation') AS LandmarkRelationBytes,
    COL_LENGTH(N'dbo.TripLandmarkCandidates', N'DistanceFromTargetMeters') AS LandmarkTargetDistanceBytes;
