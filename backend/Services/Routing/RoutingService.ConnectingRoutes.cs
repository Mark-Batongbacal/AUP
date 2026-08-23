using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
    private const int MaxBoardingVariantsPerRoute = 4;

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
                _logger.LogWarning(
                    ex,
                    "Failed to confirm trip option for route {RouteId}",
                    candidate.RouteId);
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
        .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
        .ThenBy(candidate => candidate.BoardAccess.TotalTimeSeconds)
        .FirstOrDefault();

    /// <summary>
    /// Keeps a small, deliberately diverse set of boarding variants for one
    /// jeepney route. Exact projections on the full route are injected for both
    /// boarding and alighting, while sampled anchors remain useful for broader
    /// candidate search. Exact projections can use walk or tricycle access.
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
            route.RouteId,
            samples,
            originLatitude,
            originLongitude);
        var alightAccessOptions = ComputeAlightAccessOptions(
            route.RouteId,
            samples,
            destinationLatitude,
            destinationLongitude);

        var boardCandidates = boardAccessOptions
            .Select(ConstrainTransitAccess)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToList();

        var exactBoard = BuildExactFullRouteBoardAccess(
            route.RouteId,
            samples,
            originLatitude,
            originLongitude);
        AddUniqueAccessCandidate(boardCandidates, exactBoard);

        var alightCandidates = alightAccessOptions
            .Select(ConstrainTransitAccess)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToList();

        var exactAlight = BuildExactFullRouteAlightAccess(
            route.RouteId,
            samples,
            destinationLatitude,
            destinationLongitude);
        AddUniqueAccessCandidate(alightCandidates, exactAlight);

        var all = new List<RouteConnectionCandidate>();

        foreach (var alightAccess in alightCandidates)
        {
            var alightIndex = alightAccess.RouteSampleIndex ??
                GetNearestSampleIndex(samples, alightAccess.Anchor);
            var alightAnchor = alightAccess.FullRouteAnchor ??
                GetRouteAnchor(route.RouteId, alightIndex, alightAccess.Anchor);

            foreach (var boardAccess in boardCandidates)
            {
                var boardIndex = boardAccess.RouteSampleIndex ??
                    GetNearestSampleIndex(samples, boardAccess.Anchor);
                var boardAnchor = boardAccess.FullRouteAnchor ??
                    GetRouteAnchor(route.RouteId, boardIndex, boardAccess.Anchor);

                // Direction is based on authoritative full-route progress, not
                // merely sample indices. This remains correct on bends/loops.
                var rideDistance = RouteDistanceBetweenAnchors(boardAnchor, alightAnchor);
                if (rideDistance <= 0)
                    continue;

                var jeepneyTime = JeepneyBoardingWaitTimeSeconds +
                    rideDistance / JeepneySpeedMetersPerSecond;

                var total =
                    boardAccess.GeneralizedCostPesos +
                    alightAccess.GeneralizedCostPesos +
                    GeneralizedCostFromTimeAndFare(
                        jeepneyTime,
                        JeepneyBaseFarePesos);

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

        var selected = new List<RouteConnectionCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(RouteConnectionCandidate candidate)
        {
            if (selected.Count >= MaxBoardingVariantsPerRoute)
                return;

            var key = $"{candidate.BoardIndex}:{candidate.AlightIndex}:" +
                $"{candidate.BoardAccess.Anchor.Latitude:F6}:" +
                $"{candidate.BoardAccess.Anchor.Longitude:F6}:" +
                $"{candidate.AlightAccess.Anchor.Latitude:F6}:" +
                $"{candidate.AlightAccess.Anchor.Longitude:F6}";
            if (seen.Add(key))
                selected.Add(candidate);
        }

        // 1) Nearest directionally-valid boarding opportunity on full geometry.
        Add(distinct
            .OrderBy(candidate =>
                StraightLineBoardAccessMeters(
                    candidate,
                    originLatitude,
                    originLongitude))
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
            .First());

        // 2) Best provisional balanced cost.
        Add(distinct
            .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
            .ThenBy(candidate =>
                StraightLineBoardAccessMeters(
                    candidate,
                    originLatitude,
                    originLongitude))
            .First());

        // 3) Fastest provisional complete journey.
        Add(distinct
            .OrderBy(EstimateConnectionTimeSeconds)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
            .First());

        // 4) Cheapest provisional complete journey.
        Add(distinct
            .OrderBy(EstimateConnectionFarePesos)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos)
            .First());

        if (selected.Count < MaxBoardingVariantsPerRoute)
        {
            foreach (var candidate in distinct
                         .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                         .ThenBy(candidate =>
                             StraightLineBoardAccessMeters(
                                 candidate,
                                 originLatitude,
                                 originLongitude)))
            {
                Add(candidate);
                if (selected.Count >= MaxBoardingVariantsPerRoute)
                    break;
            }
        }

        return selected;
    }

    private static void AddUniqueAccessCandidate(
        List<AccessCandidate> candidates,
        AccessCandidate? candidate)
    {
        if (candidate is null)
            return;

        var duplicate = candidates.Any(existing =>
            ApproximateDistanceMeters(
                existing.Anchor.Latitude,
                existing.Anchor.Longitude,
                candidate.Anchor.Latitude,
                candidate.Anchor.Longitude) <= 1.0);

        if (!duplicate)
            candidates.Add(candidate);
    }

    private double EstimateConnectionTimeSeconds(RouteConnectionCandidate candidate) =>
        candidate.BoardAccess.TotalTimeSeconds +
        GetJeepneyLegTimeSeconds(
            candidate.RouteId,
            candidate.BoardIndex,
            candidate.AlightIndex,
            candidate.BoardAccess.FullRouteAnchor,
            candidate.AlightAccess.FullRouteAnchor) +
        candidate.AlightAccess.TotalTimeSeconds;

    private double EstimateConnectionFarePesos(RouteConnectionCandidate candidate) =>
        candidate.BoardAccess.FarePesos +
        JeepneyBaseFarePesos +
        candidate.AlightAccess.FarePesos;

    private static double StraightLineBoardAccessMeters(
        RouteConnectionCandidate candidate,
        double originLatitude,
        double originLongitude) =>
        ApproximateDistanceMeters(
            originLatitude,
            originLongitude,
            candidate.BoardAccess.Anchor.Latitude,
            candidate.BoardAccess.Anchor.Longitude);

    private AccessCandidate? BuildExactFullRouteBoardAccess(
        string routeId,
        List<(double Latitude, double Longitude)> samples,
        double originLatitude,
        double originLongitude)
    {
        var anchor = ProjectOntoFullRoute(
            routeId,
            (originLatitude, originLongitude),
            0);
        var point = (anchor.Latitude, anchor.Longitude);
        var sampleIndex = GetNearestSampleIndex(samples, point);
        var alternatives = new List<AccessCandidate>();

        var walkDistance = ApproximateDistanceMeters(
            originLatitude,
            originLongitude,
            anchor.Latitude,
            anchor.Longitude);
        if (walkDistance <= MaxWalkAccessDistanceMeters)
        {
            alternatives.Add(WalkAccess(
                point,
                walkDistance,
                sampleIndex,
                anchor));
        }

        foreach (var trikePoint in FindNearbyTrikePoints(
                     originLatitude,
                     originLongitude))
        {
            var walkToTrikeMeters = ApproximateDistanceMeters(
                originLatitude,
                originLongitude,
                trikePoint.Latitude,
                trikePoint.Longitude);
            var rideDistanceMeters = ApproximateDistanceMeters(
                trikePoint.Latitude,
                trikePoint.Longitude,
                anchor.Latitude,
                anchor.Longitude);

            alternatives.Add(TrikeAccess(
                point,
                trikePoint,
                walkToTrikeMeters,
                rideDistanceMeters,
                sampleIndex,
                anchor));
        }

        if (alternatives.Count == 0)
            return null;

        return ConstrainTransitAccess(WithAlternatives(alternatives));
    }

    private AccessCandidate? BuildExactFullRouteAlightAccess(
        string routeId,
        List<(double Latitude, double Longitude)> samples,
        double destinationLatitude,
        double destinationLongitude)
    {
        var anchor = ProjectOntoFullRoute(
            routeId,
            (destinationLatitude, destinationLongitude),
            0);
        var point = (anchor.Latitude, anchor.Longitude);
        var sampleIndex = GetNearestSampleIndex(samples, point);
        var alternatives = new List<AccessCandidate>();

        var walkDistance = ApproximateDistanceMeters(
            anchor.Latitude,
            anchor.Longitude,
            destinationLatitude,
            destinationLongitude);
        if (walkDistance <= MaxWalkAccessDistanceMeters)
        {
            alternatives.Add(WalkAccess(
                point,
                walkDistance,
                sampleIndex,
                anchor));
        }

        foreach (var trikePoint in FindNearbyTrikePoints(
                     anchor.Latitude,
                     anchor.Longitude))
        {
            var walkToTrikeMeters = ApproximateDistanceMeters(
                anchor.Latitude,
                anchor.Longitude,
                trikePoint.Latitude,
                trikePoint.Longitude);
            var rideDistanceMeters = ApproximateDistanceMeters(
                trikePoint.Latitude,
                trikePoint.Longitude,
                destinationLatitude,
                destinationLongitude);

            alternatives.Add(TrikeAccess(
                point,
                trikePoint,
                walkToTrikeMeters,
                rideDistanceMeters,
                sampleIndex,
                anchor));
        }

        if (alternatives.Count == 0)
            return null;

        return ConstrainTransitAccess(WithAlternatives(alternatives));
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

    private bool IsTransitAccessWithinLimit(JeepneyAccessSegment access) =>
        access.Mode != AccessMode.Walk ||
        access.WalkDistanceMeters <= MaxWalkAccessDistanceMeters;

    // -------------------------------------------------------------------
    // Full journey planning
    // -------------------------------------------------------------------

}