-- Ensures Admin Jeepney Routes summary queries can filter and count geometry
-- rows without timing out on deployed databases that predate the full schema.

IF COL_LENGTH(N'dbo.TransportModes', N'ModeCode') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.TransportModes')
         AND name = N'UX_TransportModes_ModeCode')
BEGIN
    EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_TransportModes_ModeCode ON dbo.TransportModes (ModeCode);';
END;

IF COL_LENGTH(N'dbo.TransportRoutes', N'TransportModeId') IS NOT NULL
   AND COL_LENGTH(N'dbo.TransportRoutes', N'IsActive') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.TransportRoutes')
         AND name = N'IX_TransportRoutes_TransportMode')
BEGIN
    EXEC sys.sp_executesql N'CREATE INDEX IX_TransportRoutes_TransportMode ON dbo.TransportRoutes (TransportModeId, IsActive);';
END;

IF COL_LENGTH(N'dbo.RoutePoints', N'TransportRouteId') IS NOT NULL
   AND COL_LENGTH(N'dbo.RoutePoints', N'PointOrder') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.RoutePoints')
         AND name IN (N'IX_RoutePoints_Route', N'UQ_RoutePoints_RouteAndOrder'))
BEGIN
    EXEC sys.sp_executesql N'CREATE INDEX IX_RoutePoints_Route ON dbo.RoutePoints (TransportRouteId, PointOrder);';
END;

IF COL_LENGTH(N'dbo.RouteWaypoints', N'TransportRouteId') IS NOT NULL
   AND COL_LENGTH(N'dbo.RouteWaypoints', N'WaypointOrder') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.RouteWaypoints')
         AND name IN (N'IX_RouteWaypoints_Route', N'UQ_RouteWaypoints_RouteAndOrder'))
BEGIN
    EXEC sys.sp_executesql N'CREATE INDEX IX_RouteWaypoints_Route ON dbo.RouteWaypoints (TransportRouteId, WaypointOrder);';
END;
