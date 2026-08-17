using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
    public async Task<List<JeepneyTripPlan>> PlanTripsAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        var boardAccessPrefixByRoute =
            new Dictionary<string,
                (double[] Cost, AccessCandidate?[] Access)>();

        var alightAccessSuffixByRoute =
            new Dictionary<string,
                (double[] Cost, AccessCandidate?[] Access)>();

        foreach (var (routeId, samples) in _routeSamples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogWarning(
            "TRIKE DEBUG: ComputeBoardAccessOptions called for route {RouteId}",
            routeId);

            var boardOptions =
                ComputeBoardAccessOptions(
                    samples,
                    originLatitude,
                    originLongitude);

            boardAccessPrefixByRoute[routeId] =
                ComputePrefixMinAccess(boardOptions);

            var alightOptions =
                ComputeAlightAccessOptions(
                    samples,
                    destinationLatitude,
                    destinationLongitude);

            alightAccessSuffixByRoute[routeId] =
                ComputeSuffixMinAccess(alightOptions);
        }

        var candidates = new List<JourneyCandidate>();

        // 0 transfers.
        foreach (var route in _routes)
        {
            if (!_routeSamples.ContainsKey(route.RouteId))
                continue;

            var direct = FindBestConnection(
                route,
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude);

            if (direct is null)
                continue;

            var legs = new List<JourneyLegCandidate>
            {
                new(
                    direct.RouteId,
                    direct.RouteName,
                    direct.BoardAccess.Anchor,
                    direct.AlightAccess.Anchor)
            };

            candidates.Add(new JourneyCandidate(
                legs,
                direct.BoardAccess,
                direct.AlightAccess,
                [],
                direct.BoardAccess.GeneralizedCostPesos +
                direct.AlightAccess.GeneralizedCostPesos +
                GeneralizedCostFromTimeAndFare(
                    EstimateJeepneyTravelTimeSeconds(legs),
                    JeepneyBaseFarePesos)));
        }

        // 1 and 2 transfers.
        foreach (var route in _routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_routeSamples.TryGetValue(
                    route.RouteId,
                    out var samplesA))
            {
                continue;
            }

            if (!_interchangesByRoute.TryGetValue(
                    route.RouteId,
                    out var edgesFromA))
            {
                continue;
            }

            var prefixA =
                boardAccessPrefixByRoute[route.RouteId];

            foreach (var edge1 in edgesFromA)
            {
                // Boarding must occur before the first transfer.
                if (edge1.OwnIndex == 0)
                    continue;

                var boardAccess =
                    prefixA.Access[edge1.OwnIndex];

                if (boardAccess is null)
                    continue;

                var transferFromA =
                    samplesA[edge1.OwnIndex];

                var samplesB =
                    _routeSamples[edge1.OtherRouteId];

                var transferToB =
                    samplesB[edge1.OtherIndex];

                var suffixB =
                    alightAccessSuffixByRoute[
                        edge1.OtherRouteId];

                // Do not choose a transfer solely because it is
                // geographically closest. Score the complete provisional
                // journey: access + first jeepney ride + transfer walk +
                // second jeepney ride + destination access.
                var oneTransfer = BuildOneTransferCandidate(
                    route,
                    samplesA,
                    edge1,
                    boardAccess,
                    suffixB);

                if (oneTransfer is not null)
                    candidates.Add(oneTransfer);

                if (!_interchangesByRoute.TryGetValue(
                        edge1.OtherRouteId,
                        out var edgesFromB))
                {
                    continue;
                }

                foreach (var edge2 in edgesFromB)
                {
                    // On the second route, the second transfer must happen
                    // after the first transfer point.
                    if (edge2.OwnIndex <= edge1.OtherIndex)
                        continue;

                    if (edge2.OtherRouteId == route.RouteId)
                        continue;

                    var transferFromB =
                        samplesB[edge2.OwnIndex];

                    var samplesC =
                        _routeSamples[edge2.OtherRouteId];

                    var transferToC =
                        samplesC[edge2.OtherIndex];

                    var suffixC =
                        alightAccessSuffixByRoute[
                            edge2.OtherRouteId];

                    if (edge2.OtherIndex >= samplesC.Count - 1)
                        continue;

                    var alightAccessC =
                        suffixC.Access[edge2.OtherIndex];

                    if (alightAccessC is null)
                        continue;

                    var legs = new List<JourneyLegCandidate>
                    {
                        new(
                            route.RouteId,
                            route.RouteName,
                            boardAccess.Anchor,
                            transferFromA),
                        new(
                            edge1.OtherRouteId,
                            edge1.OtherRouteName,
                            transferToB,
                            transferFromB),
                        new(
                            edge2.OtherRouteId,
                            edge2.OtherRouteName,
                            transferToC,
                            alightAccessC.Anchor)
                    };

                    candidates.Add(new JourneyCandidate(
                        legs,
                        boardAccess,
                        alightAccessC,
                        [
                            new WalkSegmentCandidate(
                                transferFromA,
                                transferToB,
                                edge1.DistanceMeters),

                            new WalkSegmentCandidate(
                                transferFromB,
                                transferToC,
                                edge2.DistanceMeters)
                        ],
                        boardAccess.GeneralizedCostPesos +
                        alightAccessC.GeneralizedCostPesos +
                        GeneralizedCostFromTimeAndFare(
                            edge1.DistanceMeters /
                            WalkingSpeedMetersPerSecond,
                            0) +
                        GeneralizedCostFromTimeAndFare(
                            edge2.DistanceMeters /
                            WalkingSpeedMetersPerSecond,
                            0) +
                        GeneralizedCostFromTimeAndFare(
                            EstimateJeepneyTravelTimeSeconds(legs),
                            legs.Count * JeepneyBaseFarePesos)));
                }
            }
        }

        var ranked = candidates
            .OrderBy(candidate =>
                candidate.TotalGeneralizedCostPesos)
            .Take(MaxCandidatesToConfirm)
            .ToList();

        var confirmed =
            await ConfirmJourneyCandidatesAsync(
                ranked,
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude,
                cancellationToken);

        var directPlans =
            await ConfirmDirectTripCandidatesAsync(
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude,
                cancellationToken);

        return confirmed
            .Concat(directPlans)
            .OrderBy(plan => plan.GeneralizedCostPesos)
            .Take(MaxTripOptions)
            .ToList();
    }

    private async Task<List<JeepneyTripPlan>>
        ConfirmDirectTripCandidatesAsync(
            double originLatitude,
            double originLongitude,
            double destinationLatitude,
            double destinationLongitude,
            CancellationToken cancellationToken)
    {
        var straightLineDistance = ApproximateDistanceMeters(
            originLatitude,
            originLongitude,
            destinationLatitude,
            destinationLongitude);

        var candidates = new List<DirectTripCandidate>();

        if (straightLineDistance <= MaxWalkOnlyTripDistanceMeters)
        {
            candidates.Add(new DirectTripCandidate(
                WalkAccess(
                    (destinationLatitude, destinationLongitude),
                    straightLineDistance),
                MaxWalkOnlyTripDistanceMeters));
        }

        if (straightLineDistance <= MaxWalkTrikeTripDistanceMeters)
        {
            foreach (var trikePoint in FindNearbyTrikePoints(
                         originLatitude,
                         originLongitude))
            {
                candidates.Add(new DirectTripCandidate(
                    TrikeAccess(
                        (destinationLatitude, destinationLongitude),
                        trikePoint,
                        ApproximateDistanceMeters(
                            originLatitude,
                            originLongitude,
                            trikePoint.Latitude,
                            trikePoint.Longitude),
                        ApproximateDistanceMeters(
                            trikePoint.Latitude,
                            trikePoint.Longitude,
                            destinationLatitude,
                            destinationLongitude)),
                    MaxWalkTrikeTripDistanceMeters));
            }
        }

        var tasks = candidates.Select(async candidate =>
        {
            var access = await ConfirmAccessAsync(
                candidate.Access,
                (originLatitude, originLongitude),
                (destinationLatitude, destinationLongitude),
                cancellationToken);

            if (access is null)
                return null;

            var actualDistance = access.WalkDistanceMeters +
                (access.TrikeRideDistanceMeters ?? 0);

            var maximumDistance = access.Mode == AccessMode.Walk
                ? MaxWalkOnlyTripDistanceMeters
                : candidate.MaximumDistanceMeters;

            if (actualDistance > maximumDistance)
                return null;

            var legs = BuildAccessLegs(
                access,
                (originLatitude, originLongitude),
                (destinationLatitude, destinationLongitude));

            return new JeepneyTripPlan
            {
                Legs = legs,
                OriginAccess = access,
                DestinationAccess = EmptyAccessSegment(),
                TotalTimeSeconds = access.TotalTimeSeconds,
                TotalFarePesos = access.TotalFarePesos,
                GeneralizedCostPesos = access.GeneralizedCostPesos
            };
        });

        var results = await Task.WhenAll(tasks);

        return results
            .Where(plan => plan is not null)
            .Select(plan => plan!)
            .ToList();
    }

    private static JeepneyAccessSegment EmptyAccessSegment() =>
        new()
        {
            Mode = AccessMode.Walk,
            TotalTimeSeconds = 0,
            TotalFarePesos = 0,
            GeneralizedCostPesos = 0
        };

    /// <summary>
    /// A transfer is "useful" when it produces a good complete journey,
    /// not merely when the two route geometries are closest together.
    ///
    /// Route sample ordering is used to enforce direction: after boarding
    /// the second route, its alighting point must be ahead of the transfer.
    /// </summary>
    private JourneyCandidate? BuildOneTransferCandidate(
        StaticJeepneyRoute firstRoute,
        List<(double Latitude, double Longitude)> firstSamples,
        RouteInterchange interchange,
        AccessCandidate originAccess,
        (double[] Cost, AccessCandidate?[] Access) secondRouteSuffix)
    {
        var secondSamples =
            _routeSamples[interchange.OtherRouteId];

        if (interchange.OwnIndex <= 0 ||
            interchange.OtherIndex >= secondSamples.Count - 1)
        {
            return null;
        }

        var destinationAccess =
            secondRouteSuffix.Access[interchange.OtherIndex];

        if (destinationAccess is null)
            return null;

        var boardIndex =
            GetNearestSampleIndex(
                firstSamples,
                originAccess.Anchor);

        var alightIndex =
            GetNearestSampleIndex(
                secondSamples,
                destinationAccess.Anchor);

        // Wrong direction: the destination lies behind the transfer point.
        if (alightIndex <= interchange.OtherIndex)
            return null;

        var firstRideMeters =
            RouteDistanceBetweenSamples(
                firstSamples,
                boardIndex,
                interchange.OwnIndex);

        var secondRideMeters =
            RouteDistanceBetweenSamples(
                secondSamples,
                interchange.OtherIndex,
                alightIndex);

        var rideTime =
            (firstRideMeters + secondRideMeters) /
            JeepneySpeedMetersPerSecond;

        var transferWalkTime =
            interchange.DistanceMeters /
            WalkingSpeedMetersPerSecond;

        var provisionalCost =
            originAccess.GeneralizedCostPesos +
            destinationAccess.GeneralizedCostPesos +
            GeneralizedCostFromTimeAndFare(
                transferWalkTime,
                0) +
            GeneralizedCostFromTimeAndFare(
                rideTime +
                2 * JeepneyBoardingWaitTimeSeconds,
                2 * JeepneyBaseFarePesos);

        return new JourneyCandidate(
            [
                new JourneyLegCandidate(
                    firstRoute.RouteId,
                    firstRoute.RouteName,
                    originAccess.Anchor,
                    firstSamples[interchange.OwnIndex]),

                new JourneyLegCandidate(
                    interchange.OtherRouteId,
                    interchange.OtherRouteName,
                    secondSamples[interchange.OtherIndex],
                    destinationAccess.Anchor)
            ],
            originAccess,
            destinationAccess,
            [
                new WalkSegmentCandidate(
                    firstSamples[interchange.OwnIndex],
                    secondSamples[interchange.OtherIndex],
                    interchange.DistanceMeters)
            ],
            provisionalCost);
    }

    private static int GetNearestSampleIndex(
        List<(double Latitude, double Longitude)> samples,
        (double Latitude, double Longitude) point)
    {
        var bestIndex = 0;
        var bestDistance = double.PositiveInfinity;

        for (var i = 0; i < samples.Count; i++)
        {
            var distance = ApproximateDistanceMeters(
                point.Latitude,
                point.Longitude,
                samples[i].Latitude,
                samples[i].Longitude);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static double RouteDistanceBetweenSamples(
        List<(double Latitude, double Longitude)> samples,
        int startIndex,
        int endIndex)
    {
        if (endIndex <= startIndex)
            return 0;

        var distance = 0.0;

        for (var i = startIndex; i < endIndex; i++)
        {
            distance += ApproximateDistanceMeters(
                samples[i].Latitude,
                samples[i].Longitude,
                samples[i + 1].Latitude,
                samples[i + 1].Longitude);
        }

        return distance;
    }

}
