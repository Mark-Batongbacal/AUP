using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
    private const int MaxBoardingVariantsPerRoute = 3;
    private const double BoardingAccessEnvelopeMeters = 300.0;

    public async Task<List<JeepneyTripOption>> FindConnectingRoutesAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var candidates = new List<RouteConnectionCandidate>();

        foreach (var route in _routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_routeSamples.ContainsKey(route.RouteId))
                continue;

            candidates.AddRange(FindBestConnections(
                route,
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude));
        }

        var ranked = candidates
            .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
            .Take(MaxCandidatesToConfirm)
            .ToList();

        var confirmTasks = ranked.Select(async candidate =>
        {
            try
            {
                var boardTask = ConfirmAccessAsync(
                    candidate.BoardAccess,
                    (originLatitude, originLongitude),
                    candidate.BoardAccess.Anchor,
                    cancellationToken);

                var alightTask = ConfirmAccessAsync(
                    candidate.AlightAccess,
                    candidate.AlightAccess.Anchor,
                    (destinationLatitude, destinationLongitude),
                    cancellationToken);

                await Task.WhenAll(boardTask, alightTask);

                var board = await boardTask;
                var alight = await alightTask;

                if (board is null || alight is null ||
                    !IsTransitAccessWithinLimit(board) ||
                    !IsTransitAccessWithinLimit(alight))
                {
                    return null;
                }

                var jeepneyTime = GetJeepneyLegTimeSeconds(
                    candidate.RouteId,
                    candidate.BoardIndex,
                    candidate.AlightIndex,
                    candidate.BoardAccess.FullRouteAnchor,
                    candidate.AlightAccess.FullRouteAnchor);

                return new JeepneyTripOption
                {
                    RouteId = candidate.RouteId,
                    RouteName = candidate.RouteName,
                    BoardLatitude = candidate.BoardAccess.Anchor.Latitude,
                    BoardLongitude = candidate.BoardAccess.Anchor.Longitude,
                    BoardAccess = board,
                    AlightLatitude = candidate.AlightAccess.Anchor.Latitude,
                    AlightLongitude = candidate.AlightAccess.Anchor.Longitude,
                    AlightAccess = alight,
                    TotalTimeSeconds = board.TotalTimeSeconds + jeepneyTime + alight.TotalTimeSeconds,
                    TotalFarePesos = board.TotalFarePesos + alight.TotalFarePesos + JeepneyBaseFarePesos,
                    GeneralizedCostPesos =
                        board.GeneralizedCostPesos +
                        GeneralizedCostFromTimeAndFare(jeepneyTime, JeepneyBaseFarePesos) +
                        alight.GeneralizedCostPesos
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to confirm trip option for route {RouteId}", candidate.RouteId);
                return null;
            }
        });

        var results = await Task.WhenAll(confirmTasks);

        return results
            .Where(option => option is not null)
            .Select(option => option!)
            .GroupBy(option => option.RouteId, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(option => option.GeneralizedCostPesos)
                .ThenBy(option => option.TotalTimeSeconds)
                .ThenBy(option => option.BoardAccess.WalkDistanceMeters)
                .First())
            .OrderBy(option => option.GeneralizedCostPesos)
            .ThenBy(option => option.TotalTimeSeconds)
            .ThenBy(option => option.TotalFarePesos)
            .ThenBy(option => option.RouteId)
            .Take(MaxTripOptions)
            .ToList();
    }

    private RouteConnectionCandidate? FindBestConnection(
        StaticJeepneyRoute route,
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude) =>
        FindBestConnections(
            route,
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude)
        .FirstOrDefault();

    /// <summary>
    /// Keeps a small set of useful boarding variants for one jeepney route.
    /// Boarding opportunities that are substantially farther from the origin
    /// than the nearest directionally-valid opportunity are pruned before
    /// generalized-cost ranking. This prevents a feeder leg from chasing the
    /// same jeepney downstream merely to reduce the jeepney ride portion.
    /// </summary>
    private List<RouteConnectionCandidate> FindBestConnections(
        StaticJeepneyRoute route,
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude)
    {
        var samples = _routeSamples[route.RouteId];

        if (samples.Count < 2)
            return [];

        var boardAccessOptions = ComputeBoardAccessOptions(
            route.RouteId, samples, originLatitude, originLongitude);
        var alightAccessOptions = ComputeAlightAccessOptions(
            route.RouteId, samples, destinationLatitude, destinationLongitude);

        var boardCandidates = boardAccessOptions
            .Select(ConstrainTransitAccess)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToList();

        var nearestBoard = BuildNearestFullRouteBoardAccess(
            route.RouteId, samples, originLatitude, originLongitude);
        if (nearestBoard is not null &&
            boardCandidates.All(candidate =>
                ApproximateDistanceMeters(
                    candidate.Anchor.Latitude,
                    candidate.Anchor.Longitude,
                    nearestBoard.Anchor.Latitude,
                    nearestBoard.Anchor.Longitude) > 1.0))
        {
            boardCandidates.Add(nearestBoard);
        }

        var all = new List<RouteConnectionCandidate>();

        for (var alightIndex = 0; alightIndex < samples.Count; alightIndex++)
        {
            var alightAccess = ConstrainTransitAccess(alightAccessOptions[alightIndex]);
            if (alightAccess is null)
                continue;

            var alightAnchor = alightAccess.FullRouteAnchor ??
                GetRouteAnchor(route.RouteId, alightIndex, alightAccess.Anchor);

            foreach (var boardAccess in boardCandidates)
            {
                var boardIndex = boardAccess.RouteSampleIndex ??
                    GetNearestSampleIndex(samples, boardAccess.Anchor);
                var boardAnchor = boardAccess.FullRouteAnchor ??
                    GetRouteAnchor(route.RouteId, boardIndex, boardAccess.Anchor);

                var rideDistance = RouteDistanceBetweenAnchors(boardAnchor, alightAnchor);
                if (rideDistance <= 0)
                    continue;

                var jeepneyTime = JeepneyBoardingWaitTimeSeconds +
                    rideDistance / JeepneySpeedMetersPerSecond;

                var total =
                    boardAccess.GeneralizedCostPesos +
                    alightAccess.GeneralizedCostPesos +
                    GeneralizedCostFromTimeAndFare(jeepneyTime, JeepneyBaseFarePesos);

                all.Add(new RouteConnectionCandidate(
                    route.RouteId,
                    route.RouteName,
                    boardAccess,
                    alightAccess,
                    boardIndex,
                    alightIndex,
                    total));
            }
        }

        if (all.Count == 0)
            return [];

        var distinct = all
            .GroupBy(candidate => string.Join(':',
                candidate.BoardIndex,
                candidate.AlightIndex,
                Math.Round(candidate.BoardAccess.Anchor.Latitude, 6),
                Math.Round(candidate.BoardAccess.Anchor.Longitude, 6),
                Math.Round(candidate.AlightAccess.Anchor.Latitude, 6),
                Math.Round(candidate.AlightAccess.Anchor.Longitude, 6)))
            .Select(group => group
                .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                .First())
            .ToList();

        // Phase 1 boarding guard: compare only candidates that can actually
        // continue forward to an alighting point, then establish the nearest
        // useful boarding opportunity. A modest envelope still allows a nearby
        // intersection/stop to beat the exact projection when it is genuinely
        // more practical, while eliminating large downstream feeder detours.
        var nearestBoardDistance = distinct.Min(candidate =>
            StraightLineBoardAccessMeters(
                candidate,
                originLatitude,
                originLongitude));

        var boardingEnvelope = distinct
            .Where(candidate =>
                StraightLineBoardAccessMeters(
                    candidate,
                    originLatitude,
                    originLongitude) <=
                nearestBoardDistance + BoardingAccessEnvelopeMeters)
            .ToList();

        var ranked = boardingEnvelope
            .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
            .ThenBy(candidate =>
                StraightLineBoardAccessMeters(candidate, originLatitude, originLongitude))
            .ThenBy(candidate => ProvisionalWalkToBoardMeters(candidate.BoardAccess))
            .ToList();

        var selected = new List<RouteConnectionCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(RouteConnectionCandidate candidate)
        {
            var key = $"{candidate.BoardIndex}:{candidate.AlightIndex}:" +
                $"{candidate.BoardAccess.Anchor.Latitude:F6}:{candidate.BoardAccess.Anchor.Longitude:F6}";
            if (seen.Add(key)) selected.Add(candidate);
        }

        // Keep the closest valid board first. PlanTripsAsync currently consumes
        // FirstOrDefault(), so this ordering is intentional: the full planner
        // must not discard the nearby board in favor of a downstream one before
        // authoritative confirmation has a chance to run.
        var nearestBoardCandidate = boardingEnvelope
            .OrderBy(candidate =>
                StraightLineBoardAccessMeters(candidate, originLatitude, originLongitude))
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
            .First();
        Add(nearestBoardCandidate);

        // Preserve the best complete provisional journey inside the safe
        // boarding envelope as a separate confirmation candidate.
        Add(ranked[0]);

        foreach (var candidate in ranked)
        {
            if (selected.Count >= MaxBoardingVariantsPerRoute)
                break;
            Add(candidate);
        }

        return selected;
    }

    private static double StraightLineBoardAccessMeters(
        RouteConnectionCandidate candidate,
        double originLatitude,
        double originLongitude) =>
        ApproximateDistanceMeters(
            originLatitude,
            originLongitude,
            candidate.BoardAccess.Anchor.Latitude,
            candidate.BoardAccess.Anchor.Longitude);

    private AccessCandidate? BuildNearestFullRouteBoardAccess(
        string routeId,
        List<(double Latitude, double Longitude)> samples,
        double originLatitude,
        double originLongitude)
    {
        var anchor = ProjectOntoFullRoute(
            routeId,
            (originLatitude, originLongitude),
            0);
        var distance = ApproximateDistanceMeters(
            originLatitude,
            originLongitude,
            anchor.Latitude,
            anchor.Longitude);

        if (distance > MaxWalkAccessDistanceMeters)
            return null;

        var point = (anchor.Latitude, anchor.Longitude);
        var sampleIndex = GetNearestSampleIndex(samples, point);
        return WalkAccess(point, distance, sampleIndex, anchor);
    }

    private AccessCandidate? ConstrainTransitAccess(AccessCandidate candidate)
    {
        var alternatives = candidate.AllAlternatives
            .Where(alternative =>
                alternative.Mode != AccessMode.Walk ||
                alternative.WalkDistanceMeters <= MaxWalkAccessDistanceMeters)
            .OrderBy(alternative => alternative.GeneralizedCostPesos)
            .ThenBy(alternative => alternative.Mode)
            .ToList();

        if (alternatives.Count == 0)
            return null;

        return alternatives[0] with { Alternatives = alternatives };
    }

    private static double ProvisionalWalkToBoardMeters(AccessCandidate candidate) =>
        candidate.AllAlternatives
            .Where(alternative => alternative.Mode == AccessMode.Walk)
            .Select(alternative => alternative.WalkDistanceMeters)
            .DefaultIfEmpty(double.PositiveInfinity)
            .Min();

    private bool IsTransitAccessWithinLimit(JeepneyAccessSegment access) =>
        access.Mode != AccessMode.Walk ||
        access.WalkDistanceMeters <= MaxWalkAccessDistanceMeters;

    // -------------------------------------------------------------------
    // Full journey planning
    // -------------------------------------------------------------------

}
