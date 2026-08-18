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
        using var routingMeasurement = _telemetry.Measure("RoutePlanning");
        var areaValidation = _tripAreaValidator.ValidateTrip(
            originLatitude, originLongitude,
            destinationLatitude, destinationLongitude);
        if (!areaValidation.IsValid)
        {
            throw new RoutingValidationException(
                areaValidation.ErrorCode!, areaValidation.Message!);
        }

        await EnsureInitializedAsync(cancellationToken);

        var boardAccessPrefixByRoute =
            new Dictionary<string,
                (double[] Cost, AccessCandidate?[] Access)>();

        var alightAccessSuffixByRoute =
            new Dictionary<string,
                (double[] Cost, AccessCandidate?[] Access)>();

        foreach (var (routeId, samples) in _routeSamples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var boardOptions =
                ComputeBoardAccessOptions(
                    routeId,
                    samples,
                    originLatitude,
                    originLongitude);

            boardAccessPrefixByRoute[routeId] =
                ComputePrefixMinAccess(boardOptions);

            var alightOptions =
                ComputeAlightAccessOptions(
                    routeId,
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
                    direct.AlightAccess.Anchor,
                    direct.BoardIndex,
                    direct.AlightIndex,
                    direct.BoardAccess.FullRouteAnchor,
                    direct.AlightAccess.FullRouteAnchor)
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

        candidates.AddRange(FindTransferCandidates(
            boardAccessPrefixByRoute,
            alightAccessSuffixByRoute,
            cancellationToken));

        // Access generation stores walking and tricycle choices together on
        // one route anchor. Expand them before ranking; otherwise confirmation
        // accepts only the first valid choice and useful multimodal variants
        // (for example trike -> jeepney -> trike) disappear.
        var expandedCandidates = candidates
            .SelectMany(ExpandAccessAlternatives)
            .ToList();

        var distinctCandidates = expandedCandidates
            .GroupBy(GetJourneyCandidateKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(candidate => candidate.TotalGeneralizedCostPesos)
                .First())
            .ToList();

        var ranked = SelectCandidatesToConfirm(distinctCandidates);

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

        var distinctPlans = confirmed
            .Concat(directPlans)
            .GroupBy(GetPlanKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(plan => plan.GeneralizedCostPesos)
                .ThenBy(plan => plan.TotalTimeSeconds)
                .First())
            .ToList();

        var selectedPlans = SelectObjectivePlans(distinctPlans);
        _telemetry.Event(selectedPlans.Count == 0 ? "NoRouteFound" : "TripPlanned",
            outcome: selectedPlans.Count.ToString());
        return selectedPlans;
    }

    private static IEnumerable<JourneyCandidate> ExpandAccessAlternatives(
        JourneyCandidate candidate)
    {
        foreach (var origin in candidate.OriginAccess.AllAlternatives)
        foreach (var destination in candidate.DestinationAccess.AllAlternatives)
        {
            var standaloneOrigin = origin with { Alternatives = null };
            var standaloneDestination = destination with { Alternatives = null };
            var adjustedCost = candidate.TotalGeneralizedCostPesos
                - candidate.OriginAccess.GeneralizedCostPesos
                - candidate.DestinationAccess.GeneralizedCostPesos
                + standaloneOrigin.GeneralizedCostPesos
                + standaloneDestination.GeneralizedCostPesos;

            yield return candidate with
            {
                OriginAccess = standaloneOrigin,
                DestinationAccess = standaloneDestination,
                ProvisionalJourneyCostPesos = adjustedCost
            };
        }
    }

    private List<JourneyCandidate> SelectCandidatesToConfirm(
        List<JourneyCandidate> candidates)
    {
        if (candidates.Count <= MaxCandidatesToConfirm)
            return candidates;

        var selected = new List<JourneyCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var perObjective = Math.Max(1, MaxCandidatesToConfirm / 3);

        Add(candidates.OrderBy(candidate => candidate.TotalGeneralizedCostPesos));
        Add(candidates.OrderBy(EstimateCandidateFarePesos)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos));
        Add(candidates.OrderBy(EstimateCandidateTimeSeconds)
            .ThenBy(candidate => candidate.TotalGeneralizedCostPesos));

        if (selected.Count < MaxCandidatesToConfirm)
            Add(candidates.OrderBy(candidate => candidate.TotalGeneralizedCostPesos),
                MaxCandidatesToConfirm - selected.Count);

        return selected;

        void Add(IEnumerable<JourneyCandidate> source, int? limit = null)
        {
            var added = 0;
            foreach (var candidate in source)
            {
                if (added >= (limit ?? perObjective) ||
                    selected.Count >= MaxCandidatesToConfirm)
                    break;
                if (!seen.Add(GetJourneyCandidateKey(candidate)))
                    continue;
                selected.Add(candidate);
                added++;
            }
        }
    }

    private double EstimateCandidateTimeSeconds(JourneyCandidate candidate) =>
        candidate.OriginAccess.TotalTimeSeconds +
        candidate.DestinationAccess.TotalTimeSeconds +
        candidate.TransferWalkSegments.Sum(segment =>
            segment.StraightLineMeters / WalkingSpeedMetersPerSecond) +
        EstimateJeepneyTravelTimeSeconds(candidate.Legs);

    private double EstimateCandidateFarePesos(JourneyCandidate candidate) =>
        candidate.OriginAccess.FarePesos +
        candidate.DestinationAccess.FarePesos +
        candidate.Legs.Count * JeepneyBaseFarePesos;

    private List<JeepneyTripPlan> SelectObjectivePlans(
        List<JeepneyTripPlan> plans)
    {
        if (plans.Count == 0)
            return [];

        var cheapest = plans
            .OrderBy(plan => plan.TotalFarePesos)
            .ThenBy(plan => plan.GeneralizedCostPesos)
            .ThenBy(plan => plan.TotalTimeSeconds)
            .First();

        var fastest = plans
            .OrderBy(plan => plan.TotalTimeSeconds)
            .ThenBy(plan => plan.GeneralizedCostPesos)
            .ThenBy(plan => plan.TotalFarePesos)
            .First();

        // "Efficient" is the balanced choice. Normalizing time, fare, and
        // walking prevents pesos or seconds from dominating merely because
        // their numeric scales differ.
        var minTime = plans.Min(plan => plan.TotalTimeSeconds);
        var maxTime = plans.Max(plan => plan.TotalTimeSeconds);
        var minFare = plans.Min(plan => plan.TotalFarePesos);
        var maxFare = plans.Max(plan => plan.TotalFarePesos);
        var walking = plans.ToDictionary(
            plan => plan,
            plan => plan.Legs.Where(leg => leg.Mode == AccessMode.Walk)
                .Sum(leg => leg.DistanceMeters));
        var minWalk = walking.Values.Min();
        var maxWalk = walking.Values.Max();

        static double Normalize(double value, double min, double max) =>
            max <= min ? 0 : (value - min) / (max - min);

        var efficient = plans
            .OrderBy(plan =>
                Normalize(plan.TotalTimeSeconds, minTime, maxTime) +
                Normalize(plan.TotalFarePesos, minFare, maxFare) +
                Normalize(walking[plan], minWalk, maxWalk))
            .ThenBy(plan => plan.GeneralizedCostPesos)
            .First();

        var selected = new List<JeepneyTripPlan>();
        AddObjective(efficient, "efficient");
        AddObjective(cheapest, "cheapest");
        AddObjective(fastest, "fastest");

        foreach (var alternative in plans
                     .Except(selected)
                     .OrderBy(plan => plan.GeneralizedCostPesos)
                     .ThenBy(plan => plan.TotalTimeSeconds))
        {
            if (selected.Count >= MaxTripOptions)
                break;
            selected.Add(alternative);
        }

        return selected;

        void AddObjective(JeepneyTripPlan plan, string objective)
        {
            if (selected.Contains(plan))
            {
                plan.RecommendationType += $",{objective}";
                return;
            }

            plan.RecommendationType = objective;
            selected.Add(plan);
        }
    }

    private static string GetJourneyCandidateKey(JourneyCandidate candidate) =>
        string.Join('|', candidate.Legs.Select(leg => string.Join(':',
            leg.RouteId,
            leg.BoardIndex ?? -1,
            leg.AlightIndex ?? -1,
            Math.Round(leg.Board.Latitude, 6),
            Math.Round(leg.Board.Longitude, 6),
            Math.Round(leg.Alight.Latitude, 6),
            Math.Round(leg.Alight.Longitude, 6)))) +
        $"|{candidate.OriginAccess.Mode}:{candidate.OriginAccess.TrikePoint?.Id}" +
        $"|{candidate.DestinationAccess.Mode}:{candidate.DestinationAccess.TrikePoint?.Id}";

    private static string GetPlanKey(JeepneyTripPlan plan) =>
        string.Join('|', plan.Legs.Select(leg => string.Join(':',
            leg.Mode,
            leg.RouteId ?? string.Empty,
            Math.Round(leg.OriginLatitude, 6),
            Math.Round(leg.OriginLongitude, 6),
            Math.Round(leg.DestinationLatitude, 6),
            Math.Round(leg.DestinationLongitude, 6),
            leg.TrikePointId ?? string.Empty)));

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

            if (!IsWithinTotalWalkingLimit(legs))
                return null;

            return CreateTripPlan(
                legs,
                access,
                EmptyAccessSegment());
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
            GeneralizedCostFromWalking(
                transferWalkTime,
                interchange.DistanceMeters) +
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
                    firstSamples[interchange.OwnIndex],
                    originAccess.RouteSampleIndex,
                    interchange.OwnIndex,
                    originAccess.FullRouteAnchor,
                    GetRouteAnchor(
                        firstRoute.RouteId,
                        interchange.OwnIndex,
                        firstSamples[interchange.OwnIndex])),

                new JourneyLegCandidate(
                    interchange.OtherRouteId,
                    interchange.OtherRouteName,
                    secondSamples[interchange.OtherIndex],
                    destinationAccess.Anchor,
                    interchange.OtherIndex,
                    destinationAccess.RouteSampleIndex,
                    GetRouteAnchor(
                        interchange.OtherRouteId,
                        interchange.OtherIndex,
                        secondSamples[interchange.OtherIndex]),
                    destinationAccess.FullRouteAnchor)
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
