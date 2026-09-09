using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Integration.Database;

public sealed class SqlServerDatabaseIntegrationTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task MigratedSchema_IsQueryableThroughTukiDbContext()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TUKI_SQLSERVER_INTEGRATION_CONNECTION");
        var required = string.Equals(
            Environment.GetEnvironmentVariable("TUKI_REQUIRE_SQLSERVER_INTEGRATION"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.False(
                required,
                "TUKI_SQLSERVER_INTEGRATION_CONNECTION is required for the SQL Server integration job.");
            return;
        }

        var options = new DbContextOptionsBuilder<TukiDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new TukiDbContext(options);

        Assert.True(
            await context.Database.CanConnectAsync(),
            "TukiDbContext could not connect to the disposable SQL Server database.");

        var modeCodes = await context.TransportModes
            .AsNoTracking()
            .Select(mode => mode.Code)
            .ToListAsync();

        Assert.Contains("WALK", modeCodes);
        Assert.Contains("JEEPNEY", modeCodes);
        Assert.Contains("TRICYCLE", modeCodes);

        // TOP(1)-style reads force SQL Server to resolve every mapped column even
        // when the fresh CI database contains no application rows. These fail if
        // the committed SQL schema drifts behind the EF model.
        _ = await context.UserProfiles.AsNoTracking().Take(1).ToListAsync();
        _ = await context.TransportRoutes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Take(1)
            .ToListAsync();
        _ = await context.TripSessions.AsNoTracking().Take(1).ToListAsync();
        _ = await context.FavoriteTrips.AsNoTracking().Take(1).ToListAsync();
        _ = await context.ApiKeySessions.AsNoTracking().Take(1).ToListAsync();
        _ = await context.EmailVerificationTokens.AsNoTracking().Take(1).ToListAsync();
        _ = await context.PasswordResetTokens.AsNoTracking().Take(1).ToListAsync();
    }
}
