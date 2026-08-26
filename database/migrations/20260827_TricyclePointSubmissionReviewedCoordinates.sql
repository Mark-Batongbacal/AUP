IF OBJECT_ID(N'dbo.TricyclePointSubmissions', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.TricyclePointSubmissions', N'AdminLatitude') IS NULL
    BEGIN
        ALTER TABLE dbo.TricyclePointSubmissions
            ADD AdminLatitude DECIMAL(9,6) NULL;
    END;

    IF COL_LENGTH(N'dbo.TricyclePointSubmissions', N'AdminLongitude') IS NULL
    BEGIN
        ALTER TABLE dbo.TricyclePointSubmissions
            ADD AdminLongitude DECIMAL(9,6) NULL;
    END;
END;
GO

IF OBJECT_ID(N'dbo.TricyclePointSubmissions', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.check_constraints
       WHERE name = N'CK_TricyclePointSubmissions_AdminLatitude')
BEGIN
    ALTER TABLE dbo.TricyclePointSubmissions
        ADD CONSTRAINT CK_TricyclePointSubmissions_AdminLatitude
        CHECK (AdminLatitude IS NULL OR AdminLatitude BETWEEN -90 AND 90);
END;
GO

IF OBJECT_ID(N'dbo.TricyclePointSubmissions', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.check_constraints
       WHERE name = N'CK_TricyclePointSubmissions_AdminLongitude')
BEGIN
    ALTER TABLE dbo.TricyclePointSubmissions
        ADD CONSTRAINT CK_TricyclePointSubmissions_AdminLongitude
        CHECK (AdminLongitude IS NULL OR AdminLongitude BETWEEN -180 AND 180);
END;
GO

IF OBJECT_ID(N'dbo.TricyclePointSubmissions', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.check_constraints
       WHERE name = N'CK_TricyclePointSubmissions_AdminCoordinatePair')
BEGIN
    ALTER TABLE dbo.TricyclePointSubmissions
        ADD CONSTRAINT CK_TricyclePointSubmissions_AdminCoordinatePair
        CHECK (
            (AdminLatitude IS NULL AND AdminLongitude IS NULL)
            OR
            (AdminLatitude IS NOT NULL AND AdminLongitude IS NOT NULL)
        );
END;
GO
