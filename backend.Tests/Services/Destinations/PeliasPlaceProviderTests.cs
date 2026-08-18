using System.Net;
using System.Text;
using backend.Services.Destinations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace backend.Tests.Services.Destinations;

public sealed class PeliasPlaceProviderTests
{
    [Fact]
    public async Task Autocomplete_UsesFocusAndNormalizesGeoJson()
    {
        HttpRequestMessage? captured = null;
        var provider = Create(new StubHandler(request =>
        {
            captured = request;
            return Task.FromResult(Json("""
                {"features":[{"geometry":{"coordinates":[120.58,15.168]},"properties":{"gid":"openstreetmap:venue:1","name":"SM City Clark","label":"SM City Clark, Pampanga","layer":"venue","category":["mall"]}}]}
                """));
        }));
        var results = await provider.SearchAsync("SM Clark", new(15.1, 120.5));
        var result = Assert.Single(results);
        Assert.Equal("openstreetmap:venue:1", result.Id);
        Assert.Equal("pelias", result.Source);
        Assert.Equal("mall", result.Category);
        Assert.Contains("v1/autocomplete", captured!.RequestUri!.AbsoluteUri);
        Assert.Contains("focus.point.lat=15.1", captured.RequestUri.Query);
        Assert.Contains("focus.point.lon=120.5", captured.RequestUri.Query);
    }

    [Fact]
    public async Task Reverse_RequestsOsmVenueCandidatesAroundAlightPoint()
    {
        HttpRequestMessage? captured = null;
        var provider = Create(new StubHandler(request =>
        {
            captured = request;
            return Task.FromResult(Json("{\"features\":[]}"));
        }));
        await provider.FindNearbyVenuesAsync(15.2, 120.6);
        var query = captured!.RequestUri!.Query;
        Assert.Contains("v1/reverse", captured.RequestUri.AbsoluteUri);
        Assert.Contains("point.lat=15.2", query);
        Assert.Contains("layers=venue", query);
        Assert.Contains("sources=openstreetmap", query);
        Assert.Contains("boundary.circle.radius=1", query);
    }

    [Fact]
    public async Task Failure_IsReportedAsProviderUnavailable()
    {
        var provider = Create(new StubHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        await Assert.ThrowsAsync<DestinationProviderUnavailableException>(() =>
            provider.SearchAsync("Clark"));
    }

    private static PeliasPlaceProvider Create(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://pelias/") },
        Options.Create(new PeliasOptions
        {
            BaseUrl = "http://pelias", DestinationResultLimit = 20,
            LandmarkCandidateCount = 30, LandmarkSearchRadiusKilometers = 1
        }), NullLogger<PeliasPlaceProvider>.Instance);

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => callback(request);
    }
}
