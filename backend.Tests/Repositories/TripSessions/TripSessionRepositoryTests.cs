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

        // The DDL is hand-written because the EF model carries SQL Server
        // column types (nvarchar(max) and friends) that SQLite cannot create,
        // so EnsureCreated is not an option here. AssertMatchesEntityModel
        // below closes the gap that leaves: it fails loudly and by name when
        // the entity gains a column this table is missing, instead of letting
        // the drift surface later as a confusing "no such column" error.
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
                    ApproxFareSpent TEXT NOT NULL,
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
                    LastRerouteAt TEXT NULL,
                    RerouteCount INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                """);

            await AssertMatchesEntityModelAsync(context);
        }

        /// <summary>
        /// Compares the columns EF expects to map for TripSession against the
        /// columns the table above actually declares, so a property added to
        /// the entity without a matching column fails here by name.
        /// </summary>
        private static async Task AssertMatchesEntityModelAsync(TukiDbContext context)
        {
            var entityType = context.Model.FindEntityType(typeof(TripSession))
                ?? throw new InvalidOperationException(
                    "TripSession is not part of the EF model.");

            var expected = entityType
                .GetProperties()
                .Select(property => property.GetColumnName())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT name FROM pragma_table_info('TripSessions');";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    actual.Add(reader.GetString(0));
            }

            var missing = expected.Except(actual).OrderBy(name => name).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "The TripSessions test table has drifted from the TripSession entity. " +
                    $"Add these column(s) to CreateSchemaAsync: {string.Join(", ", missing)}.");
            }
        }
    }
}
