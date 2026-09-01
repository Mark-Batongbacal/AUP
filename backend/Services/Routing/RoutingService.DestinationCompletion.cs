using backend.Models.Routing;

namespace backend.Services.Routing;

public partial class RoutingService
{
    /// <summary>
    /// Confirms every kind of terminal edge through one pipeline boundary.
    /// Transit, direct access, and a legal prefix of an access path need
    /// different Valhalla evidence, but they are all generated from viable
    /// states and become complete plans before shared final Pareto/objective
    /// selection.
    /// </summary>
    private async Task<DestinationCompletionConfirmationResult>
        ConfirmDestinationCompletionEdgesAsync(
            IReadOnlyList<DestinationCompletionEdge> edges,
            double originLatitude,
            double originLongitude,
            double destinationLatitude,
            double destinationLongitude,
            CancellationToken cancellationToken,
            double? walkAccessDistanceLimitMeters = null)
    {
        var transitEdges = edges.OfType<JourneyCandidate>().ToList();
        var directEdges = edges
            .OfType<DirectAccessDestinationCompletionEdge>()
            .ToList();
        var accessPathEdges = edges
            .OfType<AccessPathDestinationCompletionEdge>()
            .ToList();

        var transitTask = Task.WhenAll(transitEdges.Select(async candidate =>
        {
            var plans = await ConfirmJourneyCandidatesAsync(
                [candidate],
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude,
                cancellationToken,
                walkAccessDistanceLimitMeters);
            return plans.FirstOrDefault() is { } plan
                ? new ConfirmedJourneyCandidate(candidate, plan)
                : null;
        }));
        var directTask = ConfirmDirectAccessDestinationCompletionsAsync(
            directEdges,
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude,
            cancellationToken);
        var accessPathTask = ConfirmOriginAccessPathCompletionsAsync(
            accessPathEdges,
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude,
            cancellationToken);

        await Task.WhenAll(transitTask, directTask, accessPathTask);

        return new DestinationCompletionConfirmationResult(
            (await transitTask)
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!)
                .ToList(),
            [.. await directTask, .. await accessPathTask]);
    }

    private sealed record DestinationCompletionConfirmationResult(
        List<ConfirmedJourneyCandidate> Transit,
        List<JeepneyTripPlan> AccessOnly);
}
