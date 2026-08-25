using backend.Models.Destinations;
using backend.Services.Destinations;
using backend.Services.Routing;

namespace backend.Services.Assistant;

/// <summary>
/// Place resolution used only by the planning assistant.  It deliberately
/// does not participate in the normal destination-search provider chain.
/// </summary>
public interface IAssistantPlaceResolver
{
    Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
        string query,
        DestinationSearchContext? context = null,
        CancellationToken cancellationToken = default);
}

public sealed class GoogleAssistantPlaceResolver(ITripAreaValidator areaValidator)
    : IAssistantPlaceResolver
{
    public async Task<IReadOnlyList<DestinationSearchResult>> SearchAsync(
        string query,
        DestinationSearchContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var results = await GooglePlacesSearchClient.SearchAsync(
            query, context, cancellationToken);

        // A result is usable for planning only when routing supports its
        // coordinate.  Keep the provider detail server-side; cards expose an
        // opaque selection ID instead.
        return results
            .Where(result => areaValidator.ValidateCoordinate(
                result.Latitude, result.Longitude).IsValid)
            .ToList();
    }
}
