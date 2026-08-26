SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.RecommendationLegs', N'StartRouteProgressMeters') IS NULL
        ALTER TABLE dbo.RecommendationLegs ADD StartRouteProgressMeters float NULL;

    IF COL_LENGTH(N'dbo.RecommendationLegs', N'EndRouteProgressMeters') IS NULL
        ALTER TABLE dbo.RecommendationLegs ADD EndRouteProgressMeters float NULL;

    IF COL_LENGTH(N'dbo.RecommendationLegs', N'StartsAlreadyOnboard') IS NULL
        ALTER TABLE dbo.RecommendationLegs ADD StartsAlreadyOnboard bit NOT NULL
            CONSTRAINT DF_RecommendationLegs_StartsAlreadyOnboard DEFAULT (0);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
