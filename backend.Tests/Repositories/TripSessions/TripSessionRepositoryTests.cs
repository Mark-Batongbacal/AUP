using backend.Models.Database;
using backend.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Repositories.TripSessions;

public sealed class TripSessionRepositoryTests
{
    [Fact]
    public async Task GetOwnedRecentHistoryAsync_ReturnsOnlyEndedSessionsForCurrentUser()
    {
        await using var database = await TripSessionTestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var completedId = Guid.NewGuid();
        var cancelledId = Guid.NewGuid();
        var activeId = Guid.NewGuid();

        await database.SeedSessionAsync(
            completedId,
            userId,
            recommendationId,
            TripNavigationState.Arrived,
            completedAt: new DateTime(2026, 8, 20, 3, 0, 0, DateTimeKind.Utc));
        await database.SeedSessionAsync(
            cancelledId,
            userId,
            recommendationId,
            TripNavigationState.Cancelled,
            cancelledAt: new DateTime(2026, 8, 20, 4, 0, 0, DateTimeKind.Utc));
        await database.SeedSessionAsync(
            activeId,
            userId,
            recommendationId,
            TripNavigationState.OnJeepney);
        await database.SeedSessionAsync(
            Guid.NewGuid(),
            userId,
            recommendationId,
            TripNavigationState.Planned);
        await database.SeedSessionAsync(
            Guid.NewGuid(),
            userId,
            recommendationId,
            TripNavigationState.Arrived);
        await database.SeedSessionAsync(
            Guid.NewGuid(),
            userId,
            recommendationId,
            TripNavigationState.Cancelled);
        await database.SeedSessionAsync(
            Guid.NewGuid(),
            otherUserId,
            recommendationId,
            TripNavigationState.Arrived,
            completedAt: new DateTime(2026, 8, 20, 5, 0, 0, DateTimeKind.Utc));

        await using var context = database.CreateContext();
        var repository = new TripSessionRepository(context);

        var result = await repository.GetOwnedRecentHistoryAsync(userId);

        Assert.Equal([cancelledId, completedId], result.Select(session => session.TripSessionId));
        Assert.DoesNotContain(result, session => session.TripSessionId == activeId);
    }

    private sealed class TripSessionTestDatabase : IAsyncDisposable
    {
        private readonly DbContextOptions<TukiDbContext> _options;
        private readonly SqliteConnection _connection;

        private TripSessionTestDatabase(SqliteConnection connection, DbContextOptions<TukiDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<TripSessionTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<TukiDbContext>()
                .UseSqlite(connection)
                .Options;

            var database = new TripSessionTestDatabase(connection, options);
            await database.CreateSchemaAsync();
            return database;
        }

        public TukiDbContext CreateContext() => new(_options);

        public async Task SeedSessionAsync(
            Guid sessionId,
            Guid userId,
            Guid recommendationId,
            TripNavigationState state,
            DateTime? completedAt = null,
            DateTime? cancelledAt = null)
        {
            await using var context = CreateContext();
            context.TripSessions.Add(new TripSession
                {
                    TripSessionId = sessionId,
                    UserId = userId,
                    RecommendationId = recommendationId,
                    OriginLatitude = 15.0,
                    OriginLongitude = 120.0,
                    DestinationLatitude = 15.1,
                    DestinationLongitude = 120.1,
                    DestinationName = "Market",
                    CurrentNavigationState = state,
                    CompletedAt = completedAt,
                    CancelledAt = cancelledAt,
                    CreatedAt = new DateTime(2026, 8, 20, 2, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 8, 20, 2, 0, 0, DateTimeKind.Utc),
                });
            await context.SaveChangesAsync();
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
                CREATE TABLE TripSessions
                (
                    TripSessionId TEXT NOT NULL CONSTRAINT PK_TripSessions PRIMARY KEY,
                    UserId TEXT NOT NULL,
                    RecommendationId TEXT NOT NULL,
                    OriginLatitude REAL NOT NULL,
                    OriginLongitude REAL NOT NULL,
                    DestinationLatitude REAL NOT NULL,
                    DestinationLongitude REAL NOT NULL,
                    DestinationName TEXT NULL,
                    CurrentLegIndex INTEGER NOT NULL,
                    CurrentNavigationState TEXT NOT NULL,
                    CurrentProgressMeters REAL NOT NULL,
                    CurrentRouteProgressMeters REAL NULL,
                    StartedAt TEXT NULL,
                    LastLocationAt TEXT NULL,
                    LastLatitude REAL NULL,
                    LastLongitude REAL NULL,
                    LastAccuracyMeters REAL NULL,
                    ConsecutiveStateConfirmationSamples INTEGER NOT NULL,
                    ConsecutiveOffRouteSamples INTEGER NOT NULL,
                    OffRouteSuspectedAt TEXT NULL,
                    LastRerouteReason TEXT NULL,
                    LastNavigationStatus TEXT NULL,
                    LastSpeechEventKey TEXT NULL,
                    LastSpokenInstruction TEXT NULL,
                    CompletedAt TEXT NULL,
                    CancelledAt TEXT NULL,
                    OriginalBudget TEXT NULL,
                    OriginalPreference TEXT NULL,
                    ApproxFareSpent TEXT NOT NULL DEFAULT '0',
                    LastRerouteAt TEXT NULL,
                    RerouteCount INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                """);
        }
    }
}
