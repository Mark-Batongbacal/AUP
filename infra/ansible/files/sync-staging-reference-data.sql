:ON ERROR EXIT

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF UPPER(N'$(SourceDatabase)') <> N'TUKI'
    THROW 53001, 'Reference-data source must be Tuki', 1;

IF UPPER(N'$(DestinationDatabase)') = N'TUKI'
    THROW 53002, 'Reference-data destination must not be Tuki', 1;

IF UPPER(N'$(SourceDatabase)') = UPPER(N'$(DestinationDatabase)')
    THROW 53003, 'Reference-data source and destination must differ', 1;

IF DB_ID(N'$(SourceDatabase)') IS NULL
    THROW 53004, 'Reference-data source database does not exist', 1;

IF DB_ID(N'$(DestinationDatabase)') IS NULL
    THROW 53005, 'Reference-data destination database does not exist', 1;

IF (
    SELECT COUNT(*)
    FROM [$(SourceDatabase)].sys.tables AS table_definition
    INNER JOIN [$(SourceDatabase)].sys.schemas AS schema_definition
        ON schema_definition.schema_id = table_definition.schema_id
    WHERE schema_definition.name = N'dbo'
      AND table_definition.name IN (
          N'TransportModes', N'TransportStops', N'TransportRoutes',
          N'RoutePoints', N'RouteWaypoints', N'RouteStops', N'RouteSegments',
          N'FareRules', N'TricyclePoints', N'TransferConnections'
      )
) <> 10
    THROW 53006, 'One or more approved source reference tables are missing', 1;

IF (
    SELECT COUNT(*)
    FROM [$(DestinationDatabase)].sys.tables AS table_definition
    INNER JOIN [$(DestinationDatabase)].sys.schemas AS schema_definition
        ON schema_definition.schema_id = table_definition.schema_id
    WHERE schema_definition.name = N'dbo'
      AND table_definition.name IN (
          N'TransportModes', N'TransportStops', N'TransportRoutes',
          N'RoutePoints', N'RouteWaypoints', N'RouteStops', N'RouteSegments',
          N'FareRules', N'TricyclePoints', N'TransferConnections'
      )
) <> 10
    THROW 53007, 'One or more approved destination reference tables are missing', 1;

DECLARE @UnexpectedDependencies TABLE (
    ApprovedTable sysname NOT NULL,
    UnapprovedDependency nvarchar(517) NOT NULL
);

INSERT @UnexpectedDependencies (ApprovedTable, UnapprovedDependency)
SELECT
    parent_table.name,
    QUOTENAME(referenced_schema.name) + N'.' + QUOTENAME(referenced_table.name)
FROM [$(SourceDatabase)].sys.foreign_keys AS foreign_key_definition
INNER JOIN [$(SourceDatabase)].sys.tables AS parent_table
    ON parent_table.object_id = foreign_key_definition.parent_object_id
INNER JOIN [$(SourceDatabase)].sys.schemas AS parent_schema
    ON parent_schema.schema_id = parent_table.schema_id
INNER JOIN [$(SourceDatabase)].sys.tables AS referenced_table
    ON referenced_table.object_id = foreign_key_definition.referenced_object_id
INNER JOIN [$(SourceDatabase)].sys.schemas AS referenced_schema
    ON referenced_schema.schema_id = referenced_table.schema_id
WHERE parent_schema.name = N'dbo'
  AND parent_table.name IN (
      N'TransportModes', N'TransportStops', N'TransportRoutes',
      N'RoutePoints', N'RouteWaypoints', N'RouteStops', N'RouteSegments',
      N'FareRules', N'TricyclePoints', N'TransferConnections'
  )
  AND NOT (
      referenced_schema.name = N'dbo'
      AND referenced_table.name IN (
          N'TransportModes', N'TransportStops', N'TransportRoutes',
          N'RoutePoints', N'RouteWaypoints', N'RouteStops', N'RouteSegments',
          N'FareRules', N'TricyclePoints', N'TransferConnections'
      )
  );

IF EXISTS (SELECT 1 FROM @UnexpectedDependencies)
BEGIN
    SELECT ApprovedTable, UnapprovedDependency
    FROM @UnexpectedDependencies
    ORDER BY ApprovedTable, UnapprovedDependency;

    THROW 53008, 'An approved source table depends on an unapproved table', 1;
END;
GO

USE [$(DestinationDatabase)];
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF DB_NAME() <> N'$(DestinationDatabase)'
        THROW 53009, 'Connected to the wrong destination database', 1;

    DELETE FROM dbo.TransferConnections;
    DELETE FROM dbo.FareRules;
    DELETE FROM dbo.RouteSegments;
    DELETE FROM dbo.RoutePoints;
    DELETE FROM dbo.RouteWaypoints;
    DELETE FROM dbo.RouteStops;
    DELETE FROM dbo.TricyclePoints;
    DELETE FROM dbo.TransportRoutes;
    DELETE FROM dbo.TransportStops;
    DELETE FROM dbo.TransportModes;

    SET IDENTITY_INSERT dbo.TransportModes ON;
    INSERT dbo.TransportModes (
        TransportModeId, ModeCode, ModeName, IsMotorized, AllowsLiveDriver,
        IconName, IsActive, CreatedAt
    )
    SELECT
        TransportModeId, ModeCode, ModeName, IsMotorized, AllowsLiveDriver,
        IconName, IsActive, CreatedAt
    FROM [$(SourceDatabase)].dbo.TransportModes;
    SET IDENTITY_INSERT dbo.TransportModes OFF;

    SET IDENTITY_INSERT dbo.TransportStops ON;
    INSERT dbo.TransportStops (
        TransportStopId, StopCode, StopName, Description, StopType, Address,
        Latitude, Longitude, IsActive, CreatedAt, UpdatedAt
    )
    SELECT
        TransportStopId, StopCode, StopName, Description, StopType, Address,
        Latitude, Longitude, IsActive, CreatedAt, UpdatedAt
    FROM [$(SourceDatabase)].dbo.TransportStops;
    SET IDENTITY_INSERT dbo.TransportStops OFF;

    SET IDENTITY_INSERT dbo.TransportRoutes ON;
    INSERT dbo.TransportRoutes (
        TransportRouteId, RouteCode, RouteName, TransportModeId,
        StartTransportStopId, EndTransportStopId, OriginName, DestinationName,
        DirectionName, OperatorName, Description, EncodedPolyline, BaseFare,
        EstimatedDistanceMeters, EstimatedDurationSeconds,
        EstimatedTotalMinutes, AverageWaitingTimeSeconds, ServiceStartTime,
        ServiceEndTime, AverageHeadwayMinutes, OperatesMonday, OperatesTuesday,
        OperatesWednesday, OperatesThursday, OperatesFriday, OperatesSaturday,
        OperatesSunday, IsActive, CreatedAt, UpdatedAt, ArchivedAt
    )
    SELECT
        TransportRouteId, RouteCode, RouteName, TransportModeId,
        StartTransportStopId, EndTransportStopId, OriginName, DestinationName,
        DirectionName, OperatorName, Description, EncodedPolyline, BaseFare,
        EstimatedDistanceMeters, EstimatedDurationSeconds,
        EstimatedTotalMinutes, AverageWaitingTimeSeconds, ServiceStartTime,
        ServiceEndTime, AverageHeadwayMinutes, OperatesMonday, OperatesTuesday,
        OperatesWednesday, OperatesThursday, OperatesFriday, OperatesSaturday,
        OperatesSunday, IsActive, CreatedAt, UpdatedAt, ArchivedAt
    FROM [$(SourceDatabase)].dbo.TransportRoutes;
    SET IDENTITY_INSERT dbo.TransportRoutes OFF;

    SET IDENTITY_INSERT dbo.RoutePoints ON;
    INSERT dbo.RoutePoints (
        RoutePointId, TransportRouteId, PointOrder, Latitude, Longitude, CreatedAt
    )
    SELECT
        RoutePointId, TransportRouteId, PointOrder, Latitude, Longitude, CreatedAt
    FROM [$(SourceDatabase)].dbo.RoutePoints;
    SET IDENTITY_INSERT dbo.RoutePoints OFF;

    SET IDENTITY_INSERT dbo.RouteWaypoints ON;
    INSERT dbo.RouteWaypoints (
        RouteWaypointId, TransportRouteId, WaypointOrder, Latitude, Longitude,
        CreatedAt
    )
    SELECT
        RouteWaypointId, TransportRouteId, WaypointOrder, Latitude, Longitude,
        CreatedAt
    FROM [$(SourceDatabase)].dbo.RouteWaypoints;
    SET IDENTITY_INSERT dbo.RouteWaypoints OFF;

    SET IDENTITY_INSERT dbo.RouteStops ON;
    INSERT dbo.RouteStops (
        RouteStopId, TransportRouteId, TransportStopId, StopOrder,
        EstimatedTimeFromStartSeconds, DistanceFromRouteStartMeters,
        Instructions, CanBoard, CanAlight, CreatedAt
    )
    SELECT
        RouteStopId, TransportRouteId, TransportStopId, StopOrder,
        EstimatedTimeFromStartSeconds, DistanceFromRouteStartMeters,
        Instructions, CanBoard, CanAlight, CreatedAt
    FROM [$(SourceDatabase)].dbo.RouteStops;
    SET IDENTITY_INSERT dbo.RouteStops OFF;

    SET IDENTITY_INSERT dbo.RouteSegments ON;
    INSERT dbo.RouteSegments (
        RouteSegmentId, TransportRouteId, FromRouteStopId, ToRouteStopId,
        SegmentOrder, DistanceMeters, EstimatedDurationSeconds, SegmentFare,
        IsBidirectional, IsActive, CreatedAt, UpdatedAt
    )
    SELECT
        RouteSegmentId, TransportRouteId, FromRouteStopId, ToRouteStopId,
        SegmentOrder, DistanceMeters, EstimatedDurationSeconds, SegmentFare,
        IsBidirectional, IsActive, CreatedAt, UpdatedAt
    FROM [$(SourceDatabase)].dbo.RouteSegments;
    SET IDENTITY_INSERT dbo.RouteSegments OFF;

    SET IDENTITY_INSERT dbo.FareRules ON;
    INSERT dbo.FareRules (
        FareRuleId, TransportModeId, TransportRouteId, PassengerType, FareType,
        RuleName, BaseFare, BaseDistanceKm, IncludedDistanceMeters,
        AdditionalFarePerKilometer, MinimumFare, MaximumFare, EffectiveFrom,
        EffectiveUntil, IsActive, CreatedAt
    )
    SELECT
        FareRuleId, TransportModeId, TransportRouteId, PassengerType, FareType,
        RuleName, BaseFare, BaseDistanceKm, IncludedDistanceMeters,
        AdditionalFarePerKilometer, MinimumFare, MaximumFare, EffectiveFrom,
        EffectiveUntil, IsActive, CreatedAt
    FROM [$(SourceDatabase)].dbo.FareRules;
    SET IDENTITY_INSERT dbo.FareRules OFF;

    SET IDENTITY_INSERT dbo.TricyclePoints ON;
    INSERT dbo.TricyclePoints (
        TricyclePointId, TransportStopId, PointCode, PointName, Description,
        Address, OperatorName, CenterLatitude, CenterLongitude, RadiusMeters,
        BaseFare, FarePerKilometer, AverageWaitingTimeSeconds, ServiceStartTime,
        ServiceEndTime, IsActive, CreatedAt, UpdatedAt
    )
    SELECT
        TricyclePointId, TransportStopId, PointCode, PointName, Description,
        Address, OperatorName, CenterLatitude, CenterLongitude, RadiusMeters,
        BaseFare, FarePerKilometer, AverageWaitingTimeSeconds, ServiceStartTime,
        ServiceEndTime, IsActive, CreatedAt, UpdatedAt
    FROM [$(SourceDatabase)].dbo.TricyclePoints;
    SET IDENTITY_INSERT dbo.TricyclePoints OFF;

    SET IDENTITY_INSERT dbo.TransferConnections ON;
    INSERT dbo.TransferConnections (
        TransferConnectionId, FromTransportStopId, ToTransportStopId,
        MaximumWalkingDistanceMeters, EstimatedWalkingTimeSeconds,
        Instructions, IsBidirectional, IsActive
    )
    SELECT
        TransferConnectionId, FromTransportStopId, ToTransportStopId,
        MaximumWalkingDistanceMeters, EstimatedWalkingTimeSeconds,
        Instructions, IsBidirectional, IsActive
    FROM [$(SourceDatabase)].dbo.TransferConnections;
    SET IDENTITY_INSERT dbo.TransferConnections OFF;

    IF (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.TransportModes)
        <> (SELECT COUNT_BIG(*) FROM dbo.TransportModes)
        THROW 53101, 'TransportModes count verification failed', 1;
    IF (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.TransportStops)
        <> (SELECT COUNT_BIG(*) FROM dbo.TransportStops)
        THROW 53102, 'TransportStops count verification failed', 1;
    IF (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.TransportRoutes)
        <> (SELECT COUNT_BIG(*) FROM dbo.TransportRoutes)
        THROW 53103, 'TransportRoutes count verification failed', 1;
    IF (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.RoutePoints)
        <> (SELECT COUNT_BIG(*) FROM dbo.RoutePoints)
        THROW 53104, 'RoutePoints count verification failed', 1;
    IF (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.RouteWaypoints)
        <> (SELECT COUNT_BIG(*) FROM dbo.RouteWaypoints)
        THROW 53105, 'RouteWaypoints count verification failed', 1;
    IF (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.RouteStops)
        <> (SELECT COUNT_BIG(*) FROM dbo.RouteStops)
        THROW 53106, 'RouteStops count verification failed', 1;
    IF (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.RouteSegments)
        <> (SELECT COUNT_BIG(*) FROM dbo.RouteSegments)
        THROW 53107, 'RouteSegments count verification failed', 1;
    IF (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.FareRules)
        <> (SELECT COUNT_BIG(*) FROM dbo.FareRules)
        THROW 53108, 'FareRules count verification failed', 1;
    IF (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.TricyclePoints)
        <> (SELECT COUNT_BIG(*) FROM dbo.TricyclePoints)
        THROW 53109, 'TricyclePoints count verification failed', 1;
    IF (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.TransferConnections)
        <> (SELECT COUNT_BIG(*) FROM dbo.TransferConnections)
        THROW 53110, 'TransferConnections count verification failed', 1;

    IF EXISTS (SELECT 1 FROM dbo.UserProfiles)
        THROW 53201, 'Sensitive staging table UserProfiles is not empty', 1;
    IF EXISTS (SELECT 1 FROM dbo.ApiKeySessions)
        THROW 53202, 'Sensitive staging table ApiKeySessions is not empty', 1;
    IF EXISTS (SELECT 1 FROM dbo.PassengerTrips)
        THROW 53203, 'Sensitive staging table PassengerTrips is not empty', 1;
    IF EXISTS (SELECT 1 FROM dbo.TripSessions)
        THROW 53204, 'Sensitive staging table TripSessions is not empty', 1;
    IF EXISTS (SELECT 1 FROM dbo.ChatConversations)
        THROW 53205, 'Sensitive staging table ChatConversations is not empty', 1;
    IF EXISTS (SELECT 1 FROM dbo.ChatMessages)
        THROW 53206, 'Sensitive staging table ChatMessages is not empty', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    reference_counts.TableName,
    reference_counts.SourceCount,
    reference_counts.StagingCount
FROM (
    SELECT N'TransportModes',
        (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.TransportModes),
        (SELECT COUNT_BIG(*) FROM dbo.TransportModes)
    UNION ALL SELECT N'TransportStops',
        (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.TransportStops),
        (SELECT COUNT_BIG(*) FROM dbo.TransportStops)
    UNION ALL SELECT N'TransportRoutes',
        (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.TransportRoutes),
        (SELECT COUNT_BIG(*) FROM dbo.TransportRoutes)
    UNION ALL SELECT N'RoutePoints',
        (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.RoutePoints),
        (SELECT COUNT_BIG(*) FROM dbo.RoutePoints)
    UNION ALL SELECT N'RouteWaypoints',
        (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.RouteWaypoints),
        (SELECT COUNT_BIG(*) FROM dbo.RouteWaypoints)
    UNION ALL SELECT N'RouteStops',
        (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.RouteStops),
        (SELECT COUNT_BIG(*) FROM dbo.RouteStops)
    UNION ALL SELECT N'RouteSegments',
        (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.RouteSegments),
        (SELECT COUNT_BIG(*) FROM dbo.RouteSegments)
    UNION ALL SELECT N'FareRules',
        (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.FareRules),
        (SELECT COUNT_BIG(*) FROM dbo.FareRules)
    UNION ALL SELECT N'TricyclePoints',
        (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.TricyclePoints),
        (SELECT COUNT_BIG(*) FROM dbo.TricyclePoints)
    UNION ALL SELECT N'TransferConnections',
        (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.TransferConnections),
        (SELECT COUNT_BIG(*) FROM dbo.TransferConnections)
) AS reference_counts (TableName, SourceCount, StagingCount)
ORDER BY reference_counts.TableName;

SELECT
    sensitive_counts.TableName,
    sensitive_counts.StagingCount
FROM (
    SELECT N'UserProfiles', (SELECT COUNT_BIG(*) FROM dbo.UserProfiles)
    UNION ALL SELECT N'ApiKeySessions', (SELECT COUNT_BIG(*) FROM dbo.ApiKeySessions)
    UNION ALL SELECT N'PassengerTrips', (SELECT COUNT_BIG(*) FROM dbo.PassengerTrips)
    UNION ALL SELECT N'TripSessions', (SELECT COUNT_BIG(*) FROM dbo.TripSessions)
    UNION ALL SELECT N'ChatConversations', (SELECT COUNT_BIG(*) FROM dbo.ChatConversations)
    UNION ALL SELECT N'ChatMessages', (SELECT COUNT_BIG(*) FROM dbo.ChatMessages)
) AS sensitive_counts (TableName, StagingCount)
ORDER BY sensitive_counts.TableName;
