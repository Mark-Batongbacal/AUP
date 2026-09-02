-- Persists model-reported AI token usage so Admin monitoring survives backend restarts.
-- Safe to run repeatedly.

IF OBJECT_ID(N'dbo.AiUsageEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AiUsageEvents
    (
        AiUsageEventId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_AiUsageEvents PRIMARY KEY,
        OccurredAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_AiUsageEvents_OccurredAtUtc DEFAULT SYSUTCDATETIME(),
        Source NVARCHAR(30) NOT NULL,
        Model NVARCHAR(200) NOT NULL,
        InputTokens BIGINT NOT NULL,
        OutputTokens BIGINT NOT NULL,
        InputUsdPerMillionTokens DECIMAL(18,6) NOT NULL,
        OutputUsdPerMillionTokens DECIMAL(18,6) NOT NULL,
        UsdToPhp DECIMAL(18,6) NOT NULL,
        EstimatedCostUsd DECIMAL(19,10) NOT NULL,
        EstimatedCostPhp DECIMAL(19,8) NOT NULL,
        CONSTRAINT CK_AiUsageEvents_InputTokens CHECK (InputTokens >= 0),
        CONSTRAINT CK_AiUsageEvents_OutputTokens CHECK (OutputTokens >= 0),
        CONSTRAINT CK_AiUsageEvents_InputPrice CHECK (InputUsdPerMillionTokens > 0),
        CONSTRAINT CK_AiUsageEvents_OutputPrice CHECK (OutputUsdPerMillionTokens > 0),
        CONSTRAINT CK_AiUsageEvents_UsdToPhp CHECK (UsdToPhp > 0),
        CONSTRAINT CK_AiUsageEvents_CostUsd CHECK (EstimatedCostUsd >= 0),
        CONSTRAINT CK_AiUsageEvents_CostPhp CHECK (EstimatedCostPhp >= 0)
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AiUsageEvents')
      AND name = N'IX_AiUsageEvents_OccurredAtUtc'
)
BEGIN
    CREATE INDEX IX_AiUsageEvents_OccurredAtUtc
        ON dbo.AiUsageEvents (OccurredAtUtc)
        INCLUDE (Source, Model, InputTokens, OutputTokens, EstimatedCostUsd, EstimatedCostPhp);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AiUsageEvents')
      AND name = N'IX_AiUsageEvents_SourceOccurredAtUtc'
)
BEGIN
    CREATE INDEX IX_AiUsageEvents_SourceOccurredAtUtc
        ON dbo.AiUsageEvents (Source, OccurredAtUtc);
END;
