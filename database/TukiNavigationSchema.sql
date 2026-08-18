SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TripSessions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TripSessions
        (
            TripSessionId uniqueidentifier NOT NULL CONSTRAINT DF_TripSessions_Id DEFAULT (newsequentialid()),
            UserId uniqueidentifier NOT NULL,
            RecommendationId uniqueidentifier NOT NULL,
            OriginLatitude float NOT NULL,
            OriginLongitude float NOT NULL,
            DestinationLatitude float NOT NULL,
            DestinationLongitude float NOT NULL,
            DestinationName nvarchar(250) NULL,
            CurrentLegIndex int NOT NULL CONSTRAINT DF_TripSessions_CurrentLeg DEFAULT (0),
            CurrentNavigationState nvarchar(40) NOT NULL,
            CurrentProgressMeters float NOT NULL CONSTRAINT DF_TripSessions_Progress DEFAULT (0),
            CurrentRouteProgressMeters float NULL,
            StartedAt datetime2(7) NULL,
            LastLocationAt datetime2(7) NULL,
            LastLatitude float NULL,
            LastLongitude float NULL,
            LastAccuracyMeters float NULL,
            ConsecutiveStateConfirmationSamples int NOT NULL CONSTRAINT DF_TripSessions_StateSamples DEFAULT (0),
            ConsecutiveOffRouteSamples int NOT NULL CONSTRAINT DF_TripSessions_OffRouteSamples DEFAULT (0),
            OffRouteSuspectedAt datetime2(7) NULL,
            LastRerouteReason nvarchar(50) NULL,
            LastNavigationStatus nvarchar(50) NULL,
            CompletedAt datetime2(7) NULL,
            CancelledAt datetime2(7) NULL,
            OriginalBudget decimal(10,2) NULL,
            OriginalPreference nvarchar(30) NULL,
            LastRerouteAt datetime2(7) NULL,
            RerouteCount int NOT NULL CONSTRAINT DF_TripSessions_RerouteCount DEFAULT (0),
            CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_TripSessions_CreatedAt DEFAULT (sysutcdatetime()),
            UpdatedAt datetime2(7) NOT NULL CONSTRAINT DF_TripSessions_UpdatedAt DEFAULT (sysutcdatetime()),
            CONSTRAINT PK_TripSessions PRIMARY KEY (TripSessionId),
            CONSTRAINT FK_TripSessions_UserProfiles FOREIGN KEY (UserId) REFERENCES dbo.UserProfiles (UserId),
            CONSTRAINT FK_TripSessions_RouteRecommendations FOREIGN KEY (RecommendationId) REFERENCES dbo.RouteRecommendations (RecommendationId)
        );
        CREATE INDEX IX_TripSessions_User_State ON dbo.TripSessions (UserId, CurrentNavigationState);
        CREATE INDEX IX_TripSessions_Recommendation ON dbo.TripSessions (RecommendationId);
    END;

    IF OBJECT_ID(N'dbo.NavigationInstructions', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.NavigationInstructions
        (
            NavigationInstructionId uniqueidentifier NOT NULL CONSTRAINT DF_NavigationInstructions_Id DEFAULT (newsequentialid()),
            TripSessionId uniqueidentifier NOT NULL,
            Sequence int NOT NULL,
            Type nvarchar(40) NOT NULL,
            Audience nvarchar(20) NOT NULL,
            LegIndex int NOT NULL,
            [Text] nvarchar(500) NOT NULL,
            StreetName nvarchar(250) NULL,
            SourceManeuverType int NULL,
            BeginShapeIndex int NULL,
            EndShapeIndex int NULL,
            Latitude float NULL,
            Longitude float NULL,
            DistanceFromLegStartMeters float NULL,
            DistanceFromRouteStartMeters float NULL,
            TriggerDistanceMeters float NOT NULL,
            RequiresConfirmation bit NOT NULL,
            CONSTRAINT PK_NavigationInstructions PRIMARY KEY (NavigationInstructionId),
            CONSTRAINT FK_NavigationInstructions_TripSessions FOREIGN KEY (TripSessionId)
                REFERENCES dbo.TripSessions (TripSessionId) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX UX_NavigationInstructions_SessionSequence
            ON dbo.NavigationInstructions (TripSessionId, Sequence);
    END;

    IF OBJECT_ID(N'dbo.TripLandmarkCandidates', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TripLandmarkCandidates
        (
            TripLandmarkCandidateId uniqueidentifier NOT NULL CONSTRAINT DF_TripLandmarkCandidates_Id DEFAULT (newsequentialid()),
            TripSessionId uniqueidentifier NOT NULL,
            LegIndex int NOT NULL,
            ExternalPlaceId nvarchar(250) NOT NULL,
            Name nvarchar(200) NOT NULL,
            Category nvarchar(50) NOT NULL,
            Latitude float NOT NULL,
            Longitude float NOT NULL,
            DistanceFromRouteStartMeters float NOT NULL,
            TriggerBeforeMeters float NOT NULL,
            TriggerAfterMeters float NOT NULL,
            CachedAt datetime2(7) NOT NULL CONSTRAINT DF_TripLandmarkCandidates_CachedAt DEFAULT (sysutcdatetime()),
            TriggeredAt datetime2(7) NULL,
            CONSTRAINT PK_TripLandmarkCandidates PRIMARY KEY (TripLandmarkCandidateId),
            CONSTRAINT FK_TripLandmarkCandidates_TripSessions FOREIGN KEY (TripSessionId)
                REFERENCES dbo.TripSessions (TripSessionId) ON DELETE CASCADE
        );
        CREATE INDEX IX_TripLandmarkCandidates_SessionLegProgress
            ON dbo.TripLandmarkCandidates (TripSessionId, LegIndex, DistanceFromRouteStartMeters);
        CREATE UNIQUE INDEX UX_TripLandmarkCandidates_SessionLegPlace
            ON dbo.TripLandmarkCandidates (TripSessionId, LegIndex, ExternalPlaceId);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
