using backend.Models.Routing;

namespace backend.Services.Routing;

public partial class RoutingService
{
    /// <summary>
    /// Exposes direct, former scalar-prefix, and bounded-prefix boarding
    /// selections to regression tests, including authoritative access
    /// confirmation. It is read-only and avoids duplicating private
    /// route-occurrence identity rules in tests.
    /// </summary>
    internal async Task<BoardingSelectionSnapshot> InspectBoardingSelectionAsync(
        string routeId,
        int downstreamSampleIndex,
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var route = _routes.Single(candidate => candidate.RouteId == routeId);
        var samples = _routeSamples[routeId];
        if (downstreamSampleIndex < 0 || downstreamSampleIndex >= samples.Count)
            throw new ArgumentOutOfRangeException(nameof(downstreamSampleIndex));

        var discovery = await DiscoverBoardAccessOptionsAsync(
            routeId,
            samples,
            originLatitude,
            originLongitude,
            cancellationToken);
        var direct = DistinctAccessOccurrences(FindBestConnections(
                route,
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude,
                discovery)
            .Select(candidate => candidate.BoardAccess));
        var generated = ConstrainTransitAccessOptions(discovery.Projected);
        var prefix = ComputePrefixAccessOptions(routeId, generated, direct);
        var previousScalar = generated
            .Take(downstreamSampleIndex)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .OrderBy(candidate => candidate.GeneralizedCostPesos)
            .FirstOrDefault();

        var directSnapshots = await Task.WhenAll(direct.Select(ToSnapshotAsync));
        var prefixSnapshots = await Task.WhenAll(
            prefix[downstreamSampleIndex].Select(ToSnapshotAsync));
        var previousSnapshot = previousScalar is null
            ? null
            : await ToSnapshotAsync(previousScalar);

        return new BoardingSelectionSnapshot(
            directSnapshots,
            previousSnapshot,
            prefixSnapshots);

        async Task<BoardingStateSnapshot> ToSnapshotAsync(AccessCandidate access)
        {
            var progress = access.FullRouteAnchor?.DistanceFromRouteStartMeters ??
                GetRouteAnchor(
                    routeId,
                    access.RouteSampleIndex ?? GetNearestSampleIndex(samples, access.Anchor),
                    access.Anchor).DistanceFromRouteStartMeters;
            var confirmed = await ConfirmAccessAsync(
                access,
                (originLatitude, originLongitude),
                access.Anchor,
                cancellationToken);
            return new BoardingStateSnapshot(
                access.RouteSampleIndex,
                progress,
                access.Anchor.Latitude,
                access.Anchor.Longitude,
                access.Mode,
                access.TrikePoint?.Id,
                access.TrikePoint?.Name,
                access.WalkDistanceMeters,
                access.TrikeRideDistanceMeters,
                access.FarePesos,
                access.GeneralizedCostPesos,
                confirmed?.WalkDistanceMeters,
                confirmed?.TrikeRideDistanceMeters,
                confirmed?.TotalFarePesos,
                confirmed?.GeneralizedCostPesos);
        }
    }

    /// <summary>
    /// Exposes the complete candidates emitted specifically by transfer search,
    /// before access-alternative expansion, confirmation-budget selection, or
    /// Valhalla confirmation. This lets regressions distinguish a normal
    /// destination edge on the boarded/current route from an equivalent
    /// zero-transfer candidate produced by direct-route discovery.
    /// </summary>
    internal async Task<IReadOnlyList<TransferDestinationCompletionSnapshot>>
        InspectTransferDestinationCompletionsAsync(
            double originLatitude,
            double originLongitude,
            double destinationLatitude,
            double destinationLongitude,
            CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var routesById = _routes.ToDictionary(
            route => route.RouteId,
            StringComparer.Ordinal);
        var boardPrefixes =
            new Dictionary<string, IReadOnlyList<AccessCandidate>[]>(
                StringComparer.Ordinal);
        var destinationAccess =
            new Dictionary<string, IReadOnlyList<AccessCandidate>>(
                StringComparer.Ordinal);

        foreach (var (routeId, samples) in _routeSamples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var discovery = await DiscoverBoardAccessOptionsAsync(
                routeId,
                samples,
                originLatitude,
                originLongitude,
                cancellationToken);
            var direct = FindBestConnections(
                routesById[routeId],
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude,
                discovery);
            boardPrefixes[routeId] = ComputePrefixAccessOptions(
                routeId,
                ConstrainTransitAccessOptions(discovery.Projected),
                direct.Select(candidate => candidate.BoardAccess));
            destinationAccess[routeId] = DistinctAccessOccurrences(
                ConstrainTransitAccessOptions(ComputeAlightAccessOptions(
                        routeId,
                        samples,
                        destinationLatitude,
                        destinationLongitude))
                    .Where(access => access is not null)
                    .Select(access => access!)
                    .Concat(direct.Select(candidate => candidate.AlightAccess)));
        }

        return FindTransferCandidates(
                boardPrefixes,
                destinationAccess,
                cancellationToken)
            .Select(candidate => new TransferDestinationCompletionSnapshot(
                candidate.Legs.Select(leg => leg.RouteId).ToList(),
                candidate.Legs.Select(leg => new TransitOccurrenceSnapshot(
                    leg.Board.Latitude,
                    leg.Board.Longitude,
                    GetBoardProgressMeters(leg),
                    leg.Alight.Latitude,
                    leg.Alight.Longitude,
                    GetAlightProgressMeters(leg))).ToList(),
                candidate.OriginAccess.Mode,
                candidate.OriginAccess.TrikePoint?.Id,
                candidate.TotalGeneralizedCostPesos))
            .ToList();
    }
}

internal sealed record BoardingSelectionSnapshot(
    IReadOnlyList<BoardingStateSnapshot> Direct,
    BoardingStateSnapshot? PreviousScalarPrefix,
    IReadOnlyList<BoardingStateSnapshot> TransferPrefix);

internal sealed record BoardingStateSnapshot(
    int? SampleIndex,
    double ProgressMeters,
    double Latitude,
    double Longitude,
    AccessMode Mode,
    string? TodaId,
    string? TodaName,
    double EstimatedWalkMeters,
    double? EstimatedTrikeMeters,
    double EstimatedFarePesos,
    double ProvisionalCostPesos,
    double? ConfirmedWalkMeters,
    double? ConfirmedTrikeMeters,
    double? ConfirmedFarePesos,
    double? ConfirmedCostPesos)
{
    public string OccurrenceKey => $"{Latitude:F6}:{Longitude:F6}:{ProgressMeters:F1}";
}

internal sealed record TransferDestinationCompletionSnapshot(
    IReadOnlyList<string> RouteIds,
    IReadOnlyList<TransitOccurrenceSnapshot> TransitOccurrences,
    AccessMode OriginAccessMode,
    string? OriginTodaId,
    double ProvisionalGeneralizedCostPesos);

internal sealed record TransitOccurrenceSnapshot(
    double BoardLatitude,
    double BoardLongitude,
    double BoardProgressMeters,
    double AlightLatitude,
    double AlightLongitude,
    double AlightProgressMeters);
