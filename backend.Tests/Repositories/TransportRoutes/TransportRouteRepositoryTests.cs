using backend.Models.Database;
using backend.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace backend.Tests.Repositories.TransportRoutes;

public sealed class TransportRouteRepositoryTests
{
    [Fact]
    public async Task GetAdminSummariesByTransportModeCodeAsync_ReturnsCountsWithoutSelectingGeometryColumns()
    {
        await using var database = await TransportRouteTestDatabase.CreateAsync();
        await database.SeedModeAsync(1, "JEEPNEY", "Jeepney");
        await database.SeedModeAsync(2, "TRICYCLE", "Tricycle");
        await database.SeedRouteAsync(
            10,
            transportModeId: 1,
            routeCode: "JEEP-ACTIVE",
            routeName: "Active Jeepney",
            isActive: true,
            encodedPolyline: "encoded-polyline6");
        await database.SeedRouteAsync(
            11,
            transportModeId: 1,
            routeCode: "JEEP-DRAFT",
            routeName: "Draft Jeepney",
            isActive: false,
            encodedPolyline: null);
        await database.SeedRouteAsync(
            12,
            transportModeId: 2,
            routeCode: "TRIKE-ACTIVE",
            routeName: "Active Tricycle",
            isActive: true,
            encodedPolyline: "trike-polyline");
        await database.SeedGeometryAsync(routeId: 10, pointCount: 3, waypointCount: 2);
        await database.SeedGeometryAsync(routeId: 11, pointCount: 1, waypointCount: 0);
        await database.SeedGeometryAsync(routeId: 12, pointCount: 5, waypointCount: 5);

        var sqlLogs = new List<string>();
        await using var context = database.CreateContext(sqlLogs.Add);
        var repository = new TransportRouteRepository(context);

        var summaries = await repository.GetAdminSummariesByTransportModeCodeAsync(
            "JEEPNEY",
            includeActive: true,
            includeInactive: true);

        Assert.Equal(["JEEP-ACTIVE", "JEEP-DRAFT"], summaries.Select(route => route.RouteCode));

        var active = summaries[0];
        Assert.Equal(10, active.RouteId);
        Assert.Equal("Active Jeepney", active.RouteName);
        Assert.Equal("Origin JEEP-ACTIVE", active.OriginName);
        Assert.Equal("Destination JEEP-ACTIVE", active.DestinationName);
        Assert.Equal("Outbound", active.DirectionName);
        Assert.Equal("Operator JEEP-ACTIVE", active.OperatorName);
        Assert.Equal("Description JEEP-ACTIVE", active.RouteDescription);
        Assert.Equal(13m, active.BaseFare);
        Assert.True(active.IsActive);
        Assert.Equal(3, active.PointCount);
        Assert.Equal(2, active.WaypointCount);
        Assert.True(active.HasPolyline);

        var draft = summaries[1];
        Assert.False(draft.IsActive);
        Assert.Equal(1, draft.PointCount);
        Assert.Equal(0, draft.WaypointCount);
        Assert.False(draft.HasPolyline);

        var routeListSql = string.Join('\n', sqlLogs);
        Assert.Contains("COUNT", routeListSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sqlLogs, sql =>
            sql.Contains("TransportRoutes", StringComparison.OrdinalIgnoreCase) &&
            (sql.Contains("RoutePoints", StringComparison.OrdinalIgnoreCase) ||
             sql.Contains("RouteWaypoints", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(sqlLogs, sql =>
            sql.Contains("RoutePoints", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sqlLogs, sql =>
            sql.Contains("RouteWaypoints", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Latitude", routeListSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Longitude", routeListSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PointOrder", routeListSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WaypointOrder", routeListSql, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TransportRouteTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TransportRouteTestDatabase(SqliteConnection connection)
        {
            _connection = connection;
        }

        public static async Task<TransportRouteTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var database = new TransportRouteTestDatabase(connection);
            await database.CreateSchemaAsync();
            return database;
        }

        public TukiDbContext CreateContext(Action<string>? log = null)
        {
            var builder = new DbContextOptionsBuilder<TukiDbContext>()
                .UseSqlite(_connection);

            if (log is not null)
            {
                builder.LogTo(
                    log,
                    [RelationalEventId.CommandExecuted],
                    LogLevel.Information);
            }

            return new TukiDbContext(builder.Options);
        }

        public async Task SeedModeAsync(int modeId, string modeCode, string modeName)
        {
            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO TransportModes
                    (TransportModeId, ModeCode, ModeName, IsMotorized, AllowsLiveDriver, IsActive, CreatedAt)
                VALUES
                    ({0}, {1}, {2}, 1, 0, 1, {3});
                """,
                modeId,
                modeCode,
                modeName,
                DateTime.UtcNow);
        }

        public async Task SeedRouteAsync(
            long routeId,
            int transportModeId,
            string routeCode,
            string routeName,
            bool isActive,
            string? encodedPolyline)
        {
            var encodedPolylineParameter = new SqliteParameter(
                "@encodedPolyline",
                (object?)encodedPolyline ?? DBNull.Value);

            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO TransportRoutes
                    (
                        TransportRouteId,
                        RouteCode,
                        RouteName,
                        TransportModeId,
                        OriginName,
                        DestinationName,
                        DirectionName,
                        OperatorName,
                        Description,
                        EncodedPolyline,
                        BaseFare,
                        IsActive,
                        CreatedAt,
                        UpdatedAt
                    )
                VALUES
                    ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13});
                """,
                routeId,
                routeCode,
                routeName,
                transportModeId,
                $"Origin {routeCode}",
                $"Destination {routeCode}",
                "Outbound",
                $"Operator {routeCode}",
                $"Description {routeCode}",
                encodedPolylineParameter,
                13m,
                isActive,
                DateTime.UtcNow,
                DateTime.UtcNow);
        }

        public async Task SeedGeometryAsync(long routeId, int pointCount, int waypointCount)
        {
            await using var context = CreateContext();
            for (var index = 1; index <= pointCount; index++)
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO RoutePoints
                        (TransportRouteId, PointOrder, Latitude, Longitude, CreatedAt)
                    VALUES
                        ({0}, {1}, {2}, {3}, {4});
                    """,
                    routeId,
                    index,
                    15.0 + index,
                    120.0 + index,
                    DateTime.UtcNow);
            }

            for (var index = 1; index <= waypointCount; index++)
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO RouteWaypoints
                        (TransportRouteId, WaypointOrder, Latitude, Longitude, CreatedAt)
                    VALUES
                        ({0}, {1}, {2}, {3}, {4});
                    """,
                    routeId,
                    index,
                    15.0 + index,
                    120.0 + index,
                    DateTime.UtcNow);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }

        private async Task CreateSchemaAsync()
        {
            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                """
                PRAGMA foreign_keys = ON;
                """);
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE TransportModes
                (
                    TransportModeId INTEGER NOT NULL CONSTRAINT PK_TransportModes PRIMARY KEY AUTOINCREMENT,
                    ModeCode TEXT NOT NULL,
                    ModeName TEXT NOT NULL,
                    IsMotorized INTEGER NOT NULL,
                    AllowsLiveDriver INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
                """);
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE TransportRoutes
                (
                    TransportRouteId INTEGER NOT NULL CONSTRAINT PK_TransportRoutes PRIMARY KEY AUTOINCREMENT,
                    RouteCode TEXT NOT NULL,
                    RouteName TEXT NOT NULL,
                    TransportModeId INTEGER NOT NULL,
                    OriginName TEXT NOT NULL,
                    DestinationName TEXT NOT NULL,
                    DirectionName TEXT NULL,
                    OperatorName TEXT NULL,
                    Description TEXT NULL,
                    EncodedPolyline TEXT NULL,
                    BaseFare TEXT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NULL,
                    CONSTRAINT FK_TransportRoutes_TransportModes
                        FOREIGN KEY (TransportModeId)
                        REFERENCES TransportModes (TransportModeId)
                );
                """);
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE RoutePoints
                (
                    RoutePointId INTEGER NOT NULL CONSTRAINT PK_RoutePoints PRIMARY KEY AUTOINCREMENT,
                    TransportRouteId INTEGER NOT NULL,
                    PointOrder INTEGER NOT NULL,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    CONSTRAINT FK_RoutePoints_TransportRoutes
                        FOREIGN KEY (TransportRouteId)
                        REFERENCES TransportRoutes (TransportRouteId)
                        ON DELETE CASCADE
                );
                """);
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE RouteWaypoints
                (
                    RouteWaypointId INTEGER NOT NULL CONSTRAINT PK_RouteWaypoints PRIMARY KEY AUTOINCREMENT,
                    TransportRouteId INTEGER NOT NULL,
                    WaypointOrder INTEGER NOT NULL,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    CONSTRAINT FK_RouteWaypoints_TransportRoutes
                        FOREIGN KEY (TransportRouteId)
                        REFERENCES TransportRoutes (TransportRouteId)
                        ON DELETE CASCADE
                );
                """);
        }
    }
}
