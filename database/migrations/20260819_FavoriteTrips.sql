SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.FavoriteTrips', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FavoriteTrips
    (
        FavoriteTripId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FavoriteTrips_FavoriteTripId DEFAULT NEWSEQUENTIALID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        RecommendationId UNIQUEIDENTIFIER NOT NULL,
        Note NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_FavoriteTrips_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_FavoriteTrips PRIMARY KEY (FavoriteTripId),
        CONSTRAINT FK_FavoriteTrips_UserProfiles
            FOREIGN KEY (UserId) REFERENCES dbo.UserProfiles(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_FavoriteTrips_RouteRecommendations
            FOREIGN KEY (RecommendationId) REFERENCES dbo.RouteRecommendations(RecommendationId) ON DELETE CASCADE
    );

    CREATE INDEX IX_FavoriteTrips_User ON dbo.FavoriteTrips (UserId);

    CREATE UNIQUE INDEX UX_FavoriteTrips_UserAndRecommendation ON dbo.FavoriteTrips (UserId, RecommendationId);
END;

COMMIT TRANSACTION;
