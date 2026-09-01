using backend.Models.Routing;
using backend.Services.Routing;

namespace backend.Tests.Services.Routing;

public sealed class RouteSpatialIndexTests
{
    [Fact]
    public void NearbyRouteIsReturnedAndFarRouteIsExcluded()
    {
        var index = RouteSpatialIndex.Build(
        [
            Route("near", (15.0000, 120.5000), (15.0100, 120.5000)),
            Route("far", (15.2000, 120.7000), (15.2100, 120.7000))
        ]);

        var routes = index.FindNearbyRoutes(15.0050, 120.5005, 200);

        Assert.Equal(["near"], routes);
    }

    [Fact]
    public void MultipleNearbySegmentsReturnOneRouteInNetworkOrder()
    {
        var index = RouteSpatialIndex.Build(
        [
            Route("first",
                (15.0000, 120.5000),
                (15.0010, 120.5010),
                (15.0020, 120.5000),
                (15.0010, 120.4990),
                (15.0000, 120.5000)),
            Route("second", (15.0000, 120.4995), (15.0020, 120.4995))
        ]);

        var routes = index.FindNearbyRoutes(15.0010, 120.5000, 250);

        Assert.Equal(["first", "second"], routes);
    }

    [Fact]
    public void DirectEligibilityRequiresOriginAndDestinationPlausibility()
    {
        var index = RouteSpatialIndex.Build(
        [
            Route("direct", (15.0000, 120.5000), (15.0200, 120.5000)),
            Route("origin-only", (15.0000, 120.5010), (15.0050, 120.5010)),
            Route("destination-only", (15.0150, 120.5020), (15.0200, 120.5020))
        ]);

        var origin = index.FindNearbyRoutes(15.0000, 120.5000, 300)
            .ToHashSet(StringComparer.Ordinal);
        var destination = index.FindNearbyRoutes(15.0200, 120.5000, 300)
            .ToHashSet(StringComparer.Ordinal);
        origin.IntersectWith(destination);

        Assert.Equal(["direct"], origin);
        Assert.DoesNotContain("origin-only", origin);
    }

    [Fact]
    public void TransferEndpointFilteringDoesNotRequireIntermediateRoutesNearEndpoints()
    {
        var index = RouteSpatialIndex.Build(
        [
            Route("first", (15.0000, 120.5000), (15.0100, 120.5100)),
            Route("middle", (15.0100, 120.5100), (15.0200, 120.5200)),
            Route("final", (15.0200, 120.5200), (15.0300, 120.5300))
        ]);

        var origin = index.FindNearbyRoutes(15.0000, 120.5000, 250);
        var destination = index.FindNearbyRoutes(15.0300, 120.5300, 250);

        Assert.Contains("first", origin);
        Assert.DoesNotContain("first", destination);
        Assert.Contains("final", destination);
        Assert.DoesNotContain("final", origin);
        Assert.DoesNotContain("middle", origin);
        Assert.DoesNotContain("middle", destination);
    }

    [Theory]
    [MemberData(nameof(ComplexGeometries))]
    public void ComplexRouteGeometryRemainsOnePlausibleRoute(
        StaticJeepneyRoute route,
        double queryLatitude,
        double queryLongitude)
    {
        var index = RouteSpatialIndex.Build([route]);

        var routes = index.FindNearbyRoutes(
            queryLatitude,
            queryLongitude,
            100);

        Assert.Equal([route.RouteId], routes);
    }

    [Fact]
    public async Task ConcurrentReadsAreSafeAndDeterministic()
    {
        var index = RouteSpatialIndex.Build(
        [
            Route("a", (15.0000, 120.5000), (15.0200, 120.5000)),
            Route("b", (15.0000, 120.5010), (15.0200, 120.5010))
        ]);

        var reads = await Task.WhenAll(Enumerable.Range(0, 200)
            .Select(_ => Task.Run(() => index.FindNearbyRoutes(
                15.0100,
                120.5005,
                200))));

        Assert.All(reads, routes => Assert.Equal(["a", "b"], routes));
    }

    [Fact]
    public void EmptyNetworkReturnsNoRoutes()
    {
        var index = RouteSpatialIndex.Build([]);

        Assert.Empty(index.FindNearbyRoutes(15.0, 120.5, 1_000));
    }

    [Fact]
    public void SphericalSearchEnvelopeIncludesConfiguredRadiusBoundary()
    {
        const double latitudeDeltaForOneKilometer = 0.00899;
        var index = RouteSpatialIndex.Build(
        [
            Route("boundary",
                (15.0 + latitudeDeltaForOneKilometer, 120.5),
                (15.01, 120.5))
        ]);

        Assert.Contains(
            "boundary",
            index.FindNearbyRoutes(15.0, 120.5, 1_000));
    }

    public static IEnumerable<object[]> ComplexGeometries()
    {
        yield return
        [
            Route("loop",
                (15.0000, 120.5000),
                (15.0020, 120.5020),
                (15.0040, 120.5000),
                (15.0020, 120.4980),
                (15.0000, 120.5000)),
            15.0020,
            120.5020
        ];
        yield return
        [
            Route("retraced",
                (15.0000, 120.5000),
                (15.0000, 120.5050),
                (15.0000, 120.5000)),
            15.0000,
            120.5030
        ];
        yield return
        [
            Route("self-intersecting",
                (15.0000, 120.5000),
                (15.0040, 120.5040),
                (15.0040, 120.5000),
                (15.0000, 120.5040)),
            15.0020,
            120.5020
        ];
    }

    private static StaticJeepneyRoute Route(
        string routeId,
        params (double Latitude, double Longitude)[] points) => new()
        {
            RouteId = routeId,
            RouteName = routeId,
            Coordinates = points
                .Select(point => new[] { point.Longitude, point.Latitude })
                .ToList()
        };
}
