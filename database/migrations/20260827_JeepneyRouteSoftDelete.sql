SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.TransportRoutes', N'ArchivedAt') IS NULL
    BEGIN
        ALTER TABLE dbo.TransportRoutes
            ADD ArchivedAt datetime2(7) NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_TransportRoutes_ArchivedAt'
          AND object_id = OBJECT_ID(N'dbo.TransportRoutes'))
    BEGIN
        CREATE INDEX IX_TransportRoutes_ArchivedAt
            ON dbo.TransportRoutes (ArchivedAt, TransportModeId, IsActive);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
