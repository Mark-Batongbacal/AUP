using System.Security.Cryptography;
using System.Text;
using backend.Services.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Routing;

public sealed class RoutingBenchmarkNetworkFixtureTests
{
    [Fact]
    public async Task ConfiguredFixture_LoadsOnlyWhenChecksumMatches()
    {
        var directory = Directory.CreateTempSubdirectory(
            "tuki-routing-benchmark-fixture-");
        try
        {
            var json = """
                {
                  "schemaVersion": 1,
                  "fixtureId": "test-network",
                  "routes": [{
                    "routeId": "R1",
                    "routeName": "Route 1",
                    "coordinates": [[120.5, 15.0], [120.51, 15.01]]
                  }],
                  "trikePoints": [{
                    "id": "T1",
                    "name": "TODA 1",
                    "latitude": 15.001,
                    "longitude": 120.501
                  }]
                }
                """;
            var path = Path.Combine(directory.FullName, "network.json");
            await File.WriteAllTextAsync(path, json);
            var checksum = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(value => value.ContentRootPath)
                .Returns(directory.FullName);
            var provider = new RoutingBenchmarkNetworkFixtureProvider(
                Options.Create(new RoutingBenchmarkNetworkOptions
                {
                    SnapshotPath = "network.json",
                    ExpectedSha256 = checksum
                }),
                environment.Object,
                NullLogger<RoutingBenchmarkNetworkFixtureProvider>.Instance);

            var fixture = await provider.GetFixtureAsync();

            Assert.NotNull(fixture);
            Assert.Equal("test-network", fixture.FixtureId);
            Assert.Equal(checksum, fixture.Sha256);
            Assert.Equal("R1", Assert.Single(fixture.Routes).RouteId);
            Assert.Equal("T1", Assert.Single(fixture.TrikePoints).Id);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ConfiguredFixture_RejectsChecksumMismatch()
    {
        var directory = Directory.CreateTempSubdirectory(
            "tuki-routing-benchmark-fixture-");
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory.FullName, "network.json"),
                "{}");
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(value => value.ContentRootPath)
                .Returns(directory.FullName);
            var provider = new RoutingBenchmarkNetworkFixtureProvider(
                Options.Create(new RoutingBenchmarkNetworkOptions
                {
                    SnapshotPath = "network.json",
                    ExpectedSha256 = new string('0', 64)
                }),
                environment.Object,
                NullLogger<RoutingBenchmarkNetworkFixtureProvider>.Instance);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                provider.GetFixtureAsync);

            Assert.Contains("checksum mismatch", error.Message);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
