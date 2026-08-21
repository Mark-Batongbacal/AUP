SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TripSessions', N'U') IS NULL
        THROW 50001, 'dbo.TripSessions does not exist. Apply the base/navigation schema first.', 1;

    IF COL_LENGTH(N'dbo.TripSessions', N'ApproxFareSpent') IS NULL
    BEGIN
        ALTER TABLE dbo.TripSessions
            ADD ApproxFareSpent decimal(10,2) NOT NULL
                CONSTRAINT DF_TripSessions_ApproxFareSpent DEFAULT (0);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
