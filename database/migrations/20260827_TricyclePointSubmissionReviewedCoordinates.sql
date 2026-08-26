IF OBJECT_ID(N'dbo.TricyclePointSubmissions', N'U') IS NULL
BEGIN
    RAISERROR('dbo.TricyclePointSubmissions does not exist. Apply the tricycle submission foundation migration first.', 16, 1);
    RETURN;
END;

IF COL_LENGTH(N'dbo.TricyclePointSubmissions', N'AdminLatitude') IS NULL
BEGIN
    EXEC(N'
        ALTER TABLE dbo.TricyclePointSubmissions
        ADD AdminLatitude DECIMAL(9,6) NULL;
    ');
END;

IF COL_LENGTH(N'dbo.TricyclePointSubmissions', N'AdminLongitude') IS NULL
BEGIN
    EXEC(N'
        ALTER TABLE dbo.TricyclePointSubmissions
        ADD AdminLongitude DECIMAL(9,6) NULL;
    ');
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_TricyclePointSubmissions_AdminLatitude'
      AND parent_object_id = OBJECT_ID(N'dbo.TricyclePointSubmissions'))
BEGIN
    EXEC(N'
        ALTER TABLE dbo.TricyclePointSubmissions
        ADD CONSTRAINT CK_TricyclePointSubmissions_AdminLatitude
        CHECK (AdminLatitude IS NULL OR AdminLatitude BETWEEN -90 AND 90);
    ');
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_TricyclePointSubmissions_AdminLongitude'
      AND parent_object_id = OBJECT_ID(N'dbo.TricyclePointSubmissions'))
BEGIN
    EXEC(N'
        ALTER TABLE dbo.TricyclePointSubmissions
        ADD CONSTRAINT CK_TricyclePointSubmissions_AdminLongitude
        CHECK (AdminLongitude IS NULL OR AdminLongitude BETWEEN -180 AND 180);
    ');
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_TricyclePointSubmissions_AdminCoordinatePair'
      AND parent_object_id = OBJECT_ID(N'dbo.TricyclePointSubmissions'))
BEGIN
    EXEC(N'
        ALTER TABLE dbo.TricyclePointSubmissions
        ADD CONSTRAINT CK_TricyclePointSubmissions_AdminCoordinatePair
        CHECK (
            (AdminLatitude IS NULL AND AdminLongitude IS NULL)
            OR
            (AdminLatitude IS NOT NULL AND AdminLongitude IS NOT NULL)
        );
    ');
END;
