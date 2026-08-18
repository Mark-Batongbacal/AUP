using backend.Models.Destinations;
using backend.Repositories;
using backend.Services.Routing;
using backend.Services.Telemetry;

namespace backend.Services.Destinations;

public interface IDestinationSearchProvider
{
    Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
        string query, DestinationSearchContext? context = null,
        CancellationToken cancellationToken = default);
}

public sealed record DestinationSearchContext(double? FocusLatitude = null, double? FocusLongitude = null);
public sealed class DestinationProviderUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

public interface IDestinationSearchService
{
    Task<DestinationSearchResponse> SearchAsync(
        string query, DestinationSearchContext? context = null,
        CancellationToken cancellationToken = default);
}

public sealed class DestinationSearchService(
    IEnumerable<IDestinationSearchProvider> providers,
    ITripAreaValidator areaValidator,
    ITukiTelemetry? telemetry = null) : IDestinationSearchService
{
    private readonly ITukiTelemetry _telemetry = telemetry ?? NullTukiTelemetry.Instance;
    public async Task<DestinationSearchResponse> SearchAsync(
        string query, DestinationSearchContext? context = null,
        CancellationToken cancellationToken = default)
    {
        using var measurement = _telemetry.Measure("DestinationSearch");
        var normalized = query.Trim();
        if (normalized.Length < 2)
            return new([], "INVALID_QUERY", "Enter at least two characters.");

        var results = new List<DestinationSearchResult>();
        var providerUnavailable = false;
        foreach (var provider in providers)
        {
            try
            {
                results.AddRange(await provider.SearchAsync(normalized, context, cancellationToken));
            }
            catch (DestinationProviderUnavailableException)
            {
                providerUnavailable = true;
            }
        }

        var valid = results
            .Where(result => areaValidator.ValidateCoordinate(
                result.Latitude, result.Longitude).IsValid)
            .DistinctBy(result => (result.Source, result.Id))
            .OrderByDescending(result => result.Name.Equals(
                normalized, StringComparison.OrdinalIgnoreCase))
            .ThenBy(result => result.Name)
            .ToList();

        _telemetry.Event(valid.Count switch
        {
            0 => providerUnavailable ? "PeliasDestinationSearchFailed" : "DestinationNotFound",
            1 => "DestinationResolved",
            _ => "DestinationAmbiguous"
        });
        return valid.Count == 0 && providerUnavailable
            ? new([], "SEARCH_PROVIDER_UNAVAILABLE", "Destination search is temporarily unavailable.")
            : new(valid);
    }
}

public sealed class LocalDestinationSearchProvider(
    ITransportStopRepository stopRepository,
    ITricyclePointRepository tricyclePointRepository) : IDestinationSearchProvider
{
    public async Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
        string query, DestinationSearchContext? context = null,
        CancellationToken cancellationToken = default)
    {
        // Both repositories are scoped over the same TukiDbContext. EF Core contexts do not
        // support overlapping operations, so keep the local database reads sequential.
        // Pelias remains a separate provider and does not share this context.
        var stopResults = await stopRepository.SearchByNameAsync(query, cancellationToken);
        var trikeResults = await tricyclePointRepository.GetAllActiveAsync(cancellationToken);

        var stops = stopResults.Select(stop => new DestinationSearchResult(
            $"stop:{stop.StopId}", stop.Name, stop.Latitude, stop.Longitude,
            stop.StopType.ToLowerInvariant(), "local", stop.Address));
        var trikes = trikeResults
            .Where(point => point.PointName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            (point.Address?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(point => new DestinationSearchResult(
                $"tricycle:{point.TricyclePointId}", point.PointName,
                point.CenterLatitude, point.CenterLongitude,
                "tricycle_terminal", "local", point.Address));
        return stops.Concat(trikes).ToList();
    }
}
