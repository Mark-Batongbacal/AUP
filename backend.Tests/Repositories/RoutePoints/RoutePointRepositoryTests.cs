using backend.Models.Database;
using backend.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Repositories.RoutePoints;

public sealed class RoutePointRepositoryTests
{
    [Fact]
    public async Task ReplaceForRouteAsync_WhenRouteHasExistingPoints_ReplacesThemInOrder()
    {
        // Arrange
        await using var database = await RoutePointTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new RoutePointRepository(context);
        const long routeId = 1;
        await database.SeedRouteAsync(routeId);
        await database.SeedRoutePointAsync(routeId, pointOrder: 1, latitude: 15.0, longitude: 120.0);
        await database.SeedRoutePointAsync(routeId, pointOrder: 2, latitude: 15.1, longitude: 120.1);

        var replacementPoints = new List<RoutePoint>
        {
            CreateRoutePoint(routeId, pointOrder: 1, latitude: 15.1451, longitude: 120.5880),
            CreateRoutePoint(routeId, pointOrder: 2, latitude: 15.1458, longitude: 120.5895),
            CreateRoutePoint(routeId, pointOrder: 3, latitude: 15.1469, longitude: 120.5912),
        };

        // Act
        var result = await repository.ReplaceForRouteAsync(routeId, replacementPoints);

        // Assert
        Assert.Equal([1, 2, 3], result.Select(point => point.PointOrder));
        Assert.All(result, point => Assert.True(point.RoutePointId > 0));

        await using var verificationContext = database.CreateContext();
        var persistedPoints = await verificationContext.RoutePoints
            .AsNoTracking()
            .Where(routePoint => routePoint.RouteId == routeId)
            .OrderBy(routePoint => routePoint.PointOrder)
            .ToListAsync();

        Assert.Equal(3, persistedPoints.Count);
        Assert.Equal([1, 2, 3], persistedPoints.Select(point => point.PointOrder));
        Assert.Equal(15.1451, persistedPoints[0].Latitude);
        Assert.Equal(120.5912, persistedPoints[2].Longitude);
    }

    [Fact]
    public async Task ReplaceForRouteAsync_WhenInsertFails_RollsBackExistingPoints()
    {
        // Arrange
        await using var database = await RoutePointTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var repository = new RoutePointRepository(context);
        const long routeId = 1;
        await database.SeedRouteAsync(routeId);
        await database.SeedRoutePointAsync(routeId, pointOrder: 1, latitude: 15.0, longitude: 120.0);
        await database.SeedRoutePointAsync(routeId, pointOrder: 2, latitude: 15.1, longitude: 120.1);

        var duplicateOrderPoints = new List<RoutePoint>
        {
            CreateRoutePoint(routeId, pointOrder: 1, latitude: 15.1451, longitude: 120.5880),
            CreateRoutePoint(routeId, pointOrder: 1, latitude: 15.1458, longitude: 120.5895),
        };

        // Act
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repository.ReplaceForRouteAsync(routeId, duplicateOrderPoints));

        // Assert
        await using var verificationContext = database.CreateContext();
        var persistedPoints = await verificationContext.RoutePoints
            .AsNoTracking()
            .Where(routePoint => routePoint.RouteId == routeId)
            .OrderBy(routePoint => routePoint.PointOrder)
            .ToListAsync();

        Assert.Equal(2, persistedPoints.Count);
        Assert.Equal([1, 2], persistedPoints.Select(point => point.PointOrder));
        Assert.Equal(15.0, persistedPoints[0].Latitude);
        Assert.Equal(120.1, persistedPoints[1].Longitude);
    }

    private static RoutePoint CreateRoutePoint(long routeId, int pointOrder, double latitude, double longitude) =>
        new()
        {
            RouteId = routeId,
            PointOrder = pointOrder,
            Latitude = latitude,
            Longitude = longitude,
            CreatedAt = DateTime.UtcNow,
        };

    private sealed class RoutePointTestDatabase : IAsyncDisposable
    {
        private readonly DbContextOptions<TukiDbContext> _options;
        private readonly SqliteConnection _connection;

        private RoutePointTestDatabase(SqliteConnection connection, DbContextOptions<TukiDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<RoutePointTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<TukiDbContext>()
                .UseSqlite(connection)
                .Options;

            var database = new RoutePointTestDatabase(connection, options);
            await database.CreateSchemaAsync();
            return database;
        }

        public TukiDbContext CreateContext() => new(_options);

        public async Task SeedRouteAsync(long routeId)
        {
            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO TransportRoutes (TransportRouteId, RouteCode, RouteName, TransportModeId)
                VALUES ({0}, {1}, {2}, {3});
                """,
                routeId,
                $"ROUTE-{routeId}",
                $"Route {routeId}",
                1);
        }

        public async Task SeedRoutePointAsync(long routeId, int pointOrder, double latitude, double longitude)
        {
            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO RoutePoints (TransportRouteId, PointOrder, Latitude, Longitude, CreatedAt)
                VALUES ({0}, {1}, {2}, {3}, {4});
                """,
                routeId,
                pointOrder,
                latitude,
                longitude,
                DateTime.UtcNow);
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
                CREATE TABLE TransportRoutes
                (
                    TransportRouteId INTEGER NOT NULL CONSTRAINT PK_TransportRoutes PRIMARY KEY AUTOINCREMENT,
                    RouteCode TEXT NOT NULL,
                    RouteName TEXT NOT NULL,
                    TransportModeId INTEGER NOT NULL
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
                    CONSTRAINT UQ_RoutePoints_RouteAndOrder UNIQUE (TransportRouteId, PointOrder),
                    CONSTRAINT FK_RoutePoints_TransportRoutes
                        FOREIGN KEY (TransportRouteId)
                        REFERENCES TransportRoutes (TransportRouteId)
                        ON DELETE CASCADE
                );
                """);
        }
    }
}
