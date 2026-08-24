using backend.Models.Database;
using backend.Services.Authentication.ApiKey;
using backend.Services.Authentication.Login;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Authentication.ApiKey;

public sealed class SqlServerApiKeyServiceTests
{
    [Fact]
    public async Task Create_StoresOnlyKeyHashAndResolvesOwnerFromNewContext()
    {
        await using var database = await ApiKeySessionTestDatabase.CreateAsync();
        IssuedApiKey issued;

        await using (var context = database.CreateContext())
        {
            var service = CreateService(context);
            issued = service.Create("guest:subject", TimeSpan.FromHours(24));
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.ApiKeySessions.SingleAsync();
            Assert.Equal("guest:subject", stored.CredentialOwner);
            Assert.NotEqual(issued.Value, stored.KeyHash);
            Assert.Equal(64, stored.KeyHash.Length);
            Assert.InRange(stored.ExpiresAt, issued.ExpiresAt.AddSeconds(-1), issued.ExpiresAt.AddSeconds(1));

            var service = CreateService(context);
            Assert.True(service.TryGetOwner(issued.Value, out var owner));
            Assert.Equal("guest:subject", owner);
        }
    }

    [Fact]
    public async Task Create_WithoutCustomLifetime_UsesConfiguredNormalLifetime()
    {
        await using var database = await ApiKeySessionTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = CreateService(context, apiKeyLifetimeHours: 8);

        var before = DateTimeOffset.UtcNow;
        var issued = service.Create("normal-user");
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(issued.ExpiresAt, before.AddHours(8), after.AddHours(8));
    }

    [Fact]
    public async Task TryGetOwner_WhenSessionExpired_ReturnsFalse()
    {
        await using var database = await ApiKeySessionTestDatabase.CreateAsync();
        string apiKey;

        await using (var context = database.CreateContext())
        {
            var service = CreateService(context);
            apiKey = service.Create("guest:expired", TimeSpan.FromHours(24)).Value;

            var stored = await context.ApiKeySessions.SingleAsync();
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            var service = CreateService(context);
            Assert.False(service.TryGetOwner(apiKey, out var owner));
            Assert.Null(owner);
        }
    }

    private static SqlServerApiKeyService CreateService(
        TukiDbContext context,
        int apiKeyLifetimeHours = 8) =>
        new(
            context,
            Options.Create(new LoginOptions
            {
                ApiKeyLifetimeHours = apiKeyLifetimeHours
            }));

    private sealed class ApiKeySessionTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<TukiDbContext> _options;

        private ApiKeySessionTestDatabase(
            SqliteConnection connection,
            DbContextOptions<TukiDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<ApiKeySessionTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<TukiDbContext>()
                .UseSqlite(connection)
                .Options;

            var database = new ApiKeySessionTestDatabase(connection, options);
            await database.CreateSchemaAsync();
            return database;
        }

        public TukiDbContext CreateContext() => new(_options);

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }

        private async Task CreateSchemaAsync()
        {
            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE ApiKeySessions
                (
                    ApiKeySessionId INTEGER NOT NULL CONSTRAINT PK_ApiKeySessions PRIMARY KEY AUTOINCREMENT,
                    KeyHash TEXT NOT NULL,
                    CredentialOwner TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    ExpiresAt TEXT NOT NULL,
                    RevokedAt TEXT NULL
                );
                """);
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX UX_ApiKeySessions_KeyHash
                    ON ApiKeySessions (KeyHash);
                """);
        }
    }
}
