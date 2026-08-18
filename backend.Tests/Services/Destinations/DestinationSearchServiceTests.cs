using backend.Models.Destinations;
using backend.Models.Database;
using backend.Repositories;
using backend.Services.Destinations;
using backend.Services.Routing;
using Microsoft.Extensions.Options;
using Moq;

namespace backend.Tests.Services.Destinations;

public sealed class DestinationSearchServiceTests
{
    private static DestinationSearchService Create(params DestinationSearchResult[] results)
    {
        var options = Options.Create(new RoutingOptions());
        return new DestinationSearchService(
            [new StubProvider(results)], new TripAreaValidator(options));
    }

    [Fact]
    public async Task ExactResult_IsRankedFirst()
    {
        var service = Create(
            Place("2", "SM Clark Annex"), Place("1", "SM Clark"));
        var response = await service.SearchAsync("SM Clark");
        Assert.Equal("SM Clark", response.Results[0].Name);
    }

    [Fact]
    public async Task AmbiguousResults_AreAllReturned()
    {
        var service = Create(Place("1", "Jollibee Porac"), Place("2", "Jollibee Clark"));
        var response = await service.SearchAsync("Jollibee");
        Assert.Equal(2, response.Results.Count);
    }

    [Fact]
    public async Task NoResult_ReturnsEmptyList()
    {
        var response = await Create().SearchAsync("missing");
        Assert.Empty(response.Results);
        Assert.Null(response.Error);
    }

    [Fact]
    public async Task ProviderResultOutsideServiceArea_IsExcluded()
    {
        var service = Create(new DestinationSearchResult(
            "far", "Manila", 14.5995, 120.9842, "city", "local"));
        var response = await service.SearchAsync("Manila");
        Assert.Empty(response.Results);
    }

    [Fact]
    public async Task LocalProvider_DoesNotOverlapRepositoriesSharingDbContext()
    {
        var stops = new Mock<ITransportStopRepository>();
        var tricycles = new Mock<ITricyclePointRepository>();
        var stopQueryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStopQuery = new TaskCompletionSource<List<TransportStop>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        stops.Setup(item => item.SearchByNameAsync("Clark", default))
            .Callback(() => stopQueryStarted.SetResult())
            .Returns(releaseStopQuery.Task);
        tricycles.Setup(item => item.GetAllActiveAsync(default))
            .ReturnsAsync([]);

        var search = new LocalDestinationSearchProvider(stops.Object, tricycles.Object)
            .SearchAsync("Clark");
        await stopQueryStarted.Task;

        tricycles.Verify(item => item.GetAllActiveAsync(default), Times.Never);
        releaseStopQuery.SetResult([]);
        await search;
        tricycles.Verify(item => item.GetAllActiveAsync(default), Times.Once);
    }

    private static DestinationSearchResult Place(string id, string name) =>
        new(id, name, 15.17, 120.58, "mall", "local");

    private sealed class StubProvider(IReadOnlyList<DestinationSearchResult> results)
        : IDestinationSearchProvider
    {
        public Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
            string query, DestinationSearchContext? context = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(results);
    }
}
