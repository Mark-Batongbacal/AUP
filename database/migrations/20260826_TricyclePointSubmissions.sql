IF OBJECT_ID(N'dbo.TricyclePointSubmissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TricyclePointSubmissions
    (
        TricyclePointSubmissionId BIGINT IDENTITY(1,1) NOT NULL,
        SubmittedByUserId UNIQUEIDENTIFIER NOT NULL,
        ProofImageUrl NVARCHAR(1000) NOT NULL,
        Latitude DECIMAL(9,6) NOT NULL,
        Longitude DECIMAL(9,6) NOT NULL,
        AccuracyMeters DECIMAL(8,2) NULL,
        LocationCapturedAt DATETIMEOFFSET(7) NOT NULL,
        SuggestedTodaName NVARCHAR(200) NULL,
        SuggestedLandmark NVARCHAR(300) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_TricyclePointSubmissions_Status DEFAULT N'Pending',
        AdminPointName NVARCHAR(200) NULL,
        AdminOperatorName NVARCHAR(200) NULL,
        AdminAddress NVARCHAR(500) NULL,
        AdminLandmark NVARCHAR(300) NULL,
        AdminDescription NVARCHAR(500) NULL,
        AdminNotes NVARCHAR(1000) NULL,
        ReviewedByUserId UNIQUEIDENTIFIER NULL,
        ReviewedAt DATETIMEOFFSET(7) NULL,
        PublishedTricyclePointId BIGINT NULL,
        CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_TricyclePointSubmissions_CreatedAt DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
        UpdatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_TricyclePointSubmissions_UpdatedAt DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
        CONSTRAINT PK_TricyclePointSubmissions PRIMARY KEY (TricyclePointSubmissionId),
        CONSTRAINT CK_TricyclePointSubmissions_Latitude CHECK (Latitude BETWEEN -90 AND 90),
        CONSTRAINT CK_TricyclePointSubmissions_Longitude CHECK (Longitude BETWEEN -180 AND 180),
        CONSTRAINT CK_TricyclePointSubmissions_Accuracy CHECK (AccuracyMeters IS NULL OR AccuracyMeters >= 0),
        CONSTRAINT CK_TricyclePointSubmissions_Status CHECK (Status IN (N'Pending', N'Approved', N'Rejected', N'NeedsChanges')),
        CONSTRAINT FK_TricyclePointSubmissions_SubmittedByUser FOREIGN KEY (SubmittedByUserId)
            REFERENCES dbo.UserProfiles(UserId),
        CONSTRAINT FK_TricyclePointSubmissions_ReviewedByUser FOREIGN KEY (ReviewedByUserId)
            REFERENCES dbo.UserProfiles(UserId),
        CONSTRAINT FK_TricyclePointSubmissions_PublishedTricyclePoint FOREIGN KEY (PublishedTricyclePointId)
            REFERENCES dbo.TricyclePoints(TricyclePointId)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TricyclePointSubmissions_StatusCreatedAt'
      AND object_id = OBJECT_ID(N'dbo.TricyclePointSubmissions'))
BEGIN
    CREATE INDEX IX_TricyclePointSubmissions_StatusCreatedAt
        ON dbo.TricyclePointSubmissions(Status, CreatedAt DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TricyclePointSubmissions_SubmitterCreatedAt'
      AND object_id = OBJECT_ID(N'dbo.TricyclePointSubmissions'))
BEGIN
    CREATE INDEX IX_TricyclePointSubmissions_SubmitterCreatedAt
        ON dbo.TricyclePointSubmissions(SubmittedByUserId, CreatedAt DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_TricyclePointSubmissions_Coordinates'
      AND object_id = OBJECT_ID(N'dbo.TricyclePointSubmissions'))
BEGIN
    CREATE INDEX IX_TricyclePointSubmissions_Coordinates
        ON dbo.TricyclePointSubmissions(Latitude, Longitude);
END;
GO
