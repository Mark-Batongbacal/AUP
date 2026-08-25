using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
    private async Task<List<JeepneyTripPlan>>
        ConfirmJourneyCandidatesAsync(
            List<JourneyCandidate> candidates,
            double originLatitude,
            double originLongitude,
            double destinationLatitude,
            double destinationLongitude,
            CancellationToken cancellationToken)
    {
        var tasks = candidates.Select(async candidate =>
        {
            try
            {
                var originTask = ConfirmAccessAsync(
                    candidate.OriginAccess,
                    (originLatitude, originLongitude),
                    candidate.OriginAccess.Anchor,
                    cancellationToken);

                var destinationTask = ConfirmAccessAsync(
                    candidate.DestinationAccess,
                    candidate.DestinationAccess.Anchor,
                    (destinationLatitude, destinationLongitude),
                    cancellationToken);

                var transferTasks =
                    candidate.TransferWalkSegments
                        .Select(async segment =>
                        {
                            var results =
                                await GetMatrixAsync(
                                    new ValhallaLocation
                                    {
                                        Lat = segment.From.Latitude,
                                        Lon = segment.From.Longitude
                                    },
                                    [
                                        new ValhallaLocation
                                        {
                                            Lat = segment.To.Latitude,
                                            Lon = segment.To.Longitude
                                        }
                                    ],
                                    "pedestrian",
                                    cancellationToken);

                            var result = results.FirstOrDefault(r =>
                                r.FromIndex == 0 &&
                                r.ToIndex == 0 &&
                                r.Distance is not null &&
                                r.Time is not null);

                            return result is null
                                ? ((double Distance, double Time)?)null
                                : (
                                    result.Distance!.Value * 1_000,
                                    result.Time!.Value);
                        })
                        .ToList();

                await Task.WhenAll(
                    new List<Task>
                    {
                        originTask,
                        destinationTask
                    }.Concat(transferTasks));

                var origin = await originTask;
                var destination = await destinationTask;
                var transfers = await Task.WhenAll(transferTasks);

                if (origin is null ||
                    destination is null ||
                    transfers.Any(t => t is null) ||
                    transfers.Any(t => t!.Value.Distance > MaxTransferWalkMeters))
                {
                    return null;
                }

                var transferDistances =
                    transfers
                        .Select(t => t!.Value.Distance)
                        .ToList();

                var transferTimes =
                    transfers
                        .Select(t => t!.Value.Time)
                        .ToList();

                var routeLegs = BuildCompleteLegs(
                    candidate,
                    origin,
                    destination,
                    transfers.Select(transfer => transfer!.Value).ToList(),
                    (originLatitude, originLongitude),
                    (destinationLatitude, destinationLongitude));

                if (!IsWithinTotalWalkingLimit(routeLegs))
                    return null;

                return CreateTripPlan(
                    routeLegs,
                    origin,
                    destination,
                    transferDistances,
                    transferTimes);
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to confirm journey candidate");

                return null;
            }
        });

        var results = await Task.WhenAll(tasks);

        return results
            .Where(plan => plan is not null)
            .Select(plan => plan!)
            .ToList();
    }

    private double EstimateJeepneyTravelTimeSeconds(
        IEnumerable<JourneyLegCandidate> legs)
    {
        var time = 0.0;

        foreach (var leg in legs)
        {
            if (!_routeSamples.TryGetValue(leg.RouteId, out var samples))
                continue;

            var boardIndex = leg.BoardIndex ?? GetNearestSampleIndex(samples, leg.Board);
            var alightIndex = leg.AlightIndex ?? GetNearestSampleIndex(samples, leg.Alight);
            var boardAnchor = leg.BoardFullRouteAnchor ??
                GetRouteAnchor(leg.RouteId, boardIndex, leg.Board);
            var alightAnchor = leg.AlightFullRouteAnchor ??
                GetRouteAnchor(leg.RouteId, alightIndex, leg.Alight);

            time += JeepneyBoardingWaitTimeSeconds +
                RouteDistanceBetweenAnchors(boardAnchor, alightAnchor) /
                JeepneySpeedMetersPerSecond;
        }

        return time;
    }

    private double GetJeepneyLegTimeSeconds(
        string routeId,
        int boardIndex,
        int alightIndex,
        RouteAnchor? boardFullRouteAnchor = null,
        RouteAnchor? alightFullRouteAnchor = null)
    {
        var samples = _routeSamples[routeId];
        var boardAnchor = boardFullRouteAnchor ??
            GetRouteAnchor(routeId, boardIndex, samples[boardIndex]);
        var alightAnchor = alightFullRouteAnchor ??
            GetRouteAnchor(routeId, alightIndex, samples[alightIndex]);
        return JeepneyBoardingWaitTimeSeconds +
            RouteDistanceBetweenAnchors(boardAnchor, alightAnchor) /
            JeepneySpeedMetersPerSecond;
    }

    private JeepneyTripPlan CreateTripPlan(
        List<JeepneyTripLeg> legs,
        JeepneyAccessSegment originAccess,
        JeepneyAccessSegment destinationAccess,
        List<double>? transferDistances = null,
        List<double>? transferTimes = null) =>
        new()
        {
            Legs = legs,
            OriginAccess = originAccess,
            DestinationAccess = destinationAccess,
            TransferWalkDistancesMeters = transferDistances ?? [],
            TransferWalkTimesSeconds = transferTimes ?? [],
            TotalTimeSeconds = legs.Sum(leg => leg.DurationSeconds),
            TotalFarePesos = legs.Sum(leg => leg.FarePesos),
            GeneralizedCostPesos = legs.Sum(leg => leg.GeneralizedCostPesos)
        };

    private bool IsWithinTotalWalkingLimit(IEnumerable<JeepneyTripLeg> legs) =>
        legs.Where(leg => leg.Mode == AccessMode.Walk)
            .Sum(leg => leg.DistanceMeters) <= MaxTotalWalkingMetersPerJourney;

    private List<JeepneyTripLeg> BuildCompleteLegs(
        JourneyCandidate candidate,
        JeepneyAccessSegment originAccess,
        JeepneyAccessSegment destinationAccess,
        List<(double Distance, double Time)> transfers,
        (double Latitude, double Longitude) origin,
        (double Latitude, double Longitude) destination)
    {
        var legs = BuildAccessLegs(
            originAccess,
            origin,
            candidate.Legs[0].Board);

        for (var index = 0; index < candidate.Legs.Count; index++)
        {
            legs.Add(BuildJeepneyLeg(candidate.Legs[index]));

            if (index < transfers.Count && transfers[index].Distance > 0)
            {
                var transfer = candidate.TransferWalkSegments[index];
                legs.Add(BuildWalkLeg(
                    transfer.From,
                    transfer.To,
                    transfers[index].Distance,
                    transfers[index].Time));
            }
        }

        legs.AddRange(BuildAccessLegs(
            destinationAccess,
            candidate.Legs[^1].Alight,
            destination));

        return legs;
    }

    private List<JeepneyTripLeg> BuildAccessLegs(
        JeepneyAccessSegment access,
        (double Latitude, double Longitude) from,
        (double Latitude, double Longitude) to)
    {
        if (access.Mode == AccessMode.Walk)
        {
            return access.WalkDistanceMeters <= 0
                ? []
                : [BuildWalkLeg(
                    from,
                    to,
                    access.WalkDistanceMeters,
                    access.WalkTimeSeconds)];
        }

        var trikePoint = (
            access.TrikePointLatitude!.Value,
            access.TrikePointLongitude!.Value);

        var legs = new List<JeepneyTripLeg>();

        if (access.WalkDistanceMeters > 0)
        {
            legs.Add(BuildWalkLeg(
                from,
                trikePoint,
                access.WalkDistanceMeters,
                access.WalkTimeSeconds));
        }

        legs.Add(new JeepneyTripLeg
        {
            Mode = AccessMode.Trike,
            OriginLatitude = trikePoint.Item1,
            OriginLongitude = trikePoint.Item2,
            DestinationLatitude = to.Latitude,
            DestinationLongitude = to.Longitude,
            DistanceMeters = access.TrikeRideDistanceMeters!.Value,
            DurationSeconds = access.TrikeRideTimeSeconds!.Value,
            FarePesos = access.TotalFarePesos,
            GeneralizedCostPesos = GeneralizedCostFromTimeAndFare(
                access.TrikeRideTimeSeconds.Value,
                access.TotalFarePesos),
            TrikeDistanceMeters = access.TrikeRideDistanceMeters,
            TrikeTimeSeconds = access.TrikeRideTimeSeconds,
            TrikePointId = access.TrikePointId,
            TrikePointName = access.TrikePointName
        });

        return legs;
    }

    private JeepneyTripLeg BuildJeepneyLeg(JourneyLegCandidate leg)
    {
        var samples = _routeSamples[leg.RouteId];
        var boardIndex = leg.BoardIndex ?? GetNearestSampleIndex(samples, leg.Board);
        var alightIndex = leg.AlightIndex ?? GetNearestSampleIndex(samples, leg.Alight);
        var boardAnchor = leg.BoardFullRouteAnchor ??
            GetRouteAnchor(leg.RouteId, boardIndex, leg.Board);
        var alightAnchor = leg.AlightFullRouteAnchor ??
            GetRouteAnchor(leg.RouteId, alightIndex, leg.Alight);
        var distance = RouteDistanceBetweenAnchors(boardAnchor, alightAnchor);
        var time = JeepneyBoardingWaitTimeSeconds +
            distance / JeepneySpeedMetersPerSecond;

        return new JeepneyTripLeg
        {
            Mode = AccessMode.Jeepney,
            RouteId = leg.RouteId,
            RouteName = leg.RouteName,
            BoardLatitude = leg.Board.Latitude,
            BoardLongitude = leg.Board.Longitude,
            AlightLatitude = leg.Alight.Latitude,
            AlightLongitude = leg.Alight.Longitude,
            OriginLatitude = leg.Board.Latitude,
            OriginLongitude = leg.Board.Longitude,
            DestinationLatitude = leg.Alight.Latitude,
            DestinationLongitude = leg.Alight.Longitude,
            DistanceMeters = distance,
            DurationSeconds = time,
            FarePesos = JeepneyBaseFarePesos,
            GeneralizedCostPesos = GeneralizedCostFromTimeAndFare(
                time,
                JeepneyBaseFarePesos),
            JeepneyDistanceMeters = distance,
            JeepneyTimeSeconds = time
        };
    }

    private JeepneyTripLeg BuildWalkLeg(
        (double Latitude, double Longitude) from,
        (double Latitude, double Longitude) to,
        double distance,
        double time) =>
        new()
        {
            Mode = AccessMode.Walk,
            OriginLatitude = from.Latitude,
            OriginLongitude = from.Longitude,
            DestinationLatitude = to.Latitude,
            DestinationLongitude = to.Longitude,
            DistanceMeters = distance,
            DurationSeconds = time,
            GeneralizedCostPesos = GeneralizedCostFromWalking(time, distance),
            WalkDistanceMeters = distance,
            WalkTimeSeconds = time
        };

    // -------------------------------------------------------------------
    // Trike-aware access
    // -------------------------------------------------------------------

    private async Task<JeepneyAccessSegment?> ConfirmAccessAsync(
        AccessCandidate candidate,
        (double Latitude, double Longitude) walkAnchorPoint,
        (double Latitude, double Longitude) rideTargetPoint,
        CancellationToken cancellationToken)
    {
        var alternativeIndex = 0;
        foreach (var alternative in candidate.AllAlternatives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var confirmed = await ConfirmSingleAccessAsync(
                alternative,
                walkAnchorPoint,
                rideTargetPoint,
                alternativeIndex > 0 && alternative.Mode == AccessMode.Walk
                    ? MaxWalkAccessDistanceMeters
                    : null,
                cancellationToken);

            if (confirmed is not null)
                return confirmed;

            alternativeIndex++;
        }

        return null;
    }

    private async Task<JeepneyAccessSegment?>
        ConfirmSingleAccessAsync(
            AccessCandidate candidate,
            (double Latitude, double Longitude) walkAnchorPoint,
            (double Latitude, double Longitude) rideTargetPoint,
            double? fallbackWalkMaximumDistanceMeters,
            CancellationToken cancellationToken)
    {
        if (candidate.Mode == AccessMode.Walk)
        {
            return await ConfirmWalkingAccessAsync(
                walkAnchorPoint,
                rideTargetPoint,
                fallbackWalkMaximumDistanceMeters,
                cancellationToken);
        }

        var trikePoint = candidate.TrikePoint!;

        var walkTask =
            GetMatrixAsync(
                new ValhallaLocation
                {
                    Lat = walkAnchorPoint.Latitude,
                    Lon = walkAnchorPoint.Longitude
                },
                [
                    new ValhallaLocation
                    {
                        Lat = trikePoint.Latitude,
                        Lon = trikePoint.Longitude
                    }
                ],
                "pedestrian",
                cancellationToken);

        var rideTask =
            GetMatrixAsync(
                new ValhallaLocation
                {
                    Lat = trikePoint.Latitude,
                    Lon = trikePoint.Longitude
                },
                [
                    new ValhallaLocation
                    {
                        Lat = rideTargetPoint.Latitude,
                        Lon = rideTargetPoint.Longitude
                    }
                ],
                TrikeCostingModel,
                cancellationToken);

        await Task.WhenAll(walkTask, rideTask);

        var walkResult = (await walkTask).FirstOrDefault(r =>
            r.FromIndex == 0 &&
            r.ToIndex == 0 &&
            r.Distance is not null &&
            r.Time is not null);

        var rideResult = (await rideTask).FirstOrDefault(r =>
            r.FromIndex == 0 &&
            r.ToIndex == 0 &&
            r.Distance is not null &&
            r.Time is not null);

        if (walkResult is null || rideResult is null)
            return null;

        var walkDistance =
            walkResult.Distance!.Value * 1_000;

        var walkTime = walkResult.Time!.Value;

        // Geometric proximity is only candidate generation. Validate the
        // actual pedestrian route to the terminal before accepting a trike.
        if (walkDistance > MaxWalkToTrikePointMeters)
            return null;

        var rideDistance =
            rideResult.Distance!.Value * 1_000;

        var rideTime = rideResult.Time!.Value;

        if (rideDistance <= 0)
            return null;

        var fare = ComputeTrikeFarePesos(rideDistance);

        var totalTime = walkTime + rideTime;

        return new JeepneyAccessSegment
        {
            Mode = AccessMode.Trike,
            WalkDistanceMeters = walkDistance,
            WalkTimeSeconds = walkTime,
            TrikePointId = trikePoint.Id,
            TrikePointName = trikePoint.Name,
            TrikePointLatitude = trikePoint.Latitude,
            TrikePointLongitude = trikePoint.Longitude,
            TrikeRideDistanceMeters = rideDistance,
            TrikeRideTimeSeconds = rideTime,
            TotalTimeSeconds = totalTime,
            TotalFarePesos = fare,
            GeneralizedCostPesos =
                GeneralizedCostFromTimeAndFare(
                    totalTime,
                    fare) +
                walkDistance / 1_000 * WalkingFatiguePesosPerKilometer
        };
    }

    private async Task<JeepneyAccessSegment?> ConfirmWalkingAccessAsync(
        (double Latitude, double Longitude) from,
        (double Latitude, double Longitude) to,
        double? maximumDistanceMeters,
        CancellationToken cancellationToken)
    {
        var results = await GetMatrixAsync(
            new ValhallaLocation { Lat = from.Latitude, Lon = from.Longitude },
            [new ValhallaLocation { Lat = to.Latitude, Lon = to.Longitude }],
            "pedestrian",
            cancellationToken);

        var result = results.FirstOrDefault(r =>
            r.FromIndex == 0 && r.ToIndex == 0 &&
            r.Distance is not null && r.Time is not null);

        if (result is null)
            return null;

        var distance = result.Distance!.Value * 1_000;

        if (maximumDistanceMeters is not null &&
            distance > maximumDistanceMeters.Value)
        {
            return null;
        }

        var time = result.Time!.Value;

        return new JeepneyAccessSegment
        {
            Mode = AccessMode.Walk,
            WalkDistanceMeters = distance,
            WalkTimeSeconds = time,
            TotalTimeSeconds = time,
            TotalFarePesos = 0,
            GeneralizedCostPesos = GeneralizedCostFromWalking(time, distance)
        };
    }

    private AccessCandidate[] ComputeBoardAccessOptions(
        string routeId,
        List<(double Latitude, double Longitude)> samples,
        double originLatitude,
        double originLongitude)
    {
        var trikeCandidates =
            FindNearbyTrikePoints(
                originLatitude,
                originLongitude);

        var options =
            new AccessCandidate[samples.Count];

        for (var i = 0; i < samples.Count; i++)
        {
            var fullAnchor = ProjectOntoSearchRegion(
                routeId,
                i,
                (originLatitude, originLongitude));
            options[i] = BuildBoardAccessOption(
                fullAnchor,
                i,
                originLatitude,
                originLongitude,
                trikeCandidates);
        }

        return options;
    }

    private AccessCandidate[] ComputeSearchAnchorBoardAccessOptions(
        string routeId,
        double originLatitude,
        double originLongitude)
    {
        var trikeCandidates = FindNearbyTrikePoints(
            originLatitude,
            originLongitude);

        return _routeSearchAnchors[routeId]
            .Select((anchor, sampleIndex) => BuildBoardAccessOption(
                anchor,
                sampleIndex,
                originLatitude,
                originLongitude,
                trikeCandidates))
            .ToArray();
    }

    private AccessCandidate BuildBoardAccessOption(
        RouteAnchor fullAnchor,
        int sampleIndex,
        double originLatitude,
        double originLongitude,
        IReadOnlyList<TrikePoint> trikeCandidates)
    {
        var anchor = (fullAnchor.Latitude, fullAnchor.Longitude);
        var directDistance = ApproximateDistanceMeters(
            originLatitude,
            originLongitude,
            anchor.Latitude,
            anchor.Longitude);
        var alternatives = new List<AccessCandidate>
        {
            WalkAccess(anchor, directDistance, sampleIndex, fullAnchor)
        };

        // Trike points are candidates only. The geometric ranking here is
        // deliberately cheap; the selected option is confirmed through real
        // Valhalla walking + road routing later.
        foreach (var candidate in trikeCandidates)
        {
            var walkToTrikeMeters = ApproximateDistanceMeters(
                originLatitude,
                originLongitude,
                candidate.Latitude,
                candidate.Longitude);
            var rideDistance = ApproximateDistanceMeters(
                candidate.Latitude,
                candidate.Longitude,
                anchor.Latitude,
                anchor.Longitude);

            alternatives.Add(TrikeAccess(
                anchor,
                candidate,
                walkToTrikeMeters,
                rideDistance,
                sampleIndex,
                fullAnchor));
        }

        return WithAlternatives(alternatives);
    }

    private AccessCandidate[] ComputeAlightAccessOptions(
        string routeId,
        List<(double Latitude, double Longitude)> samples,
        double destinationLatitude,
        double destinationLongitude)
    {
        var options =
            new AccessCandidate[samples.Count];

        for (var i = 0; i < samples.Count; i++)
        {
            var fullAnchor = ProjectOntoSearchRegion(
                routeId,
                i,
                (destinationLatitude, destinationLongitude));
            var anchor = (fullAnchor.Latitude, fullAnchor.Longitude);

            var directDistance =
                ApproximateDistanceMeters(
                    anchor.Latitude,
                    anchor.Longitude,
                    destinationLatitude,
                    destinationLongitude);

            var alternatives = new List<AccessCandidate>
            {
                WalkAccess(anchor, directDistance, i, fullAnchor)
            };

            var trikeCandidates =
                FindNearbyTrikePoints(
                    anchor.Latitude,
                    anchor.Longitude);

            foreach (var trikePoint in trikeCandidates)
            {
                var walkToTrikeMeters =
                    ApproximateDistanceMeters(
                        anchor.Latitude,
                        anchor.Longitude,
                        trikePoint.Latitude,
                        trikePoint.Longitude);

                var rideDistance =
                    ApproximateDistanceMeters(
                        trikePoint.Latitude,
                        trikePoint.Longitude,
                        destinationLatitude,
                        destinationLongitude);

                var trikeOption =
                    TrikeAccess(
                        anchor,
                        trikePoint,
                        walkToTrikeMeters,
                        rideDistance,
                        i,
                        fullAnchor);

                alternatives.Add(trikeOption);
            }

            options[i] = WithAlternatives(alternatives);
        }

        return options;
    }

    private List<TrikePoint> FindNearbyTrikePoints(
        double latitude,
        double longitude)
    {
        return _trikePoints
            .Select(point => new
            {
                Point = point,
                Distance = ApproximateDistanceMeters(
                    latitude,
                    longitude,
                    point.Latitude,
                    point.Longitude)
            })
            .Where(candidate =>
                candidate.Distance <=
                MaxWalkToTrikePointMeters)
            .OrderBy(candidate => candidate.Distance)
            .Take(MaxNearbyTrikeCandidates)
            .Select(candidate => candidate.Point)
            .ToList();
    }

    private AccessCandidate WithAlternatives(List<AccessCandidate> alternatives)
    {
        var ordered = alternatives
            .OrderBy(candidate => candidate.GeneralizedCostPesos)
            .ThenBy(candidate => candidate.Mode)
            .ToList();

        return ordered[0] with { Alternatives = ordered };
    }

    private AccessCandidate WalkAccess(
        (double Latitude, double Longitude) anchor,
        double distanceMeters,
        int? routeSampleIndex = null,
        RouteAnchor? fullRouteAnchor = null)
    {
        var time =
            distanceMeters /
            WalkingSpeedMetersPerSecond;

        return new AccessCandidate(
            AccessMode.Walk,
            anchor,
            distanceMeters,
            time,
            null,
            null,
            null,
            null,
            ValueOfTimePesosPerMinute,
            WalkingFatiguePesosPerKilometer,
            routeSampleIndex,
            fullRouteAnchor);
    }

    private AccessCandidate TrikeAccess(
        (double Latitude, double Longitude) anchor,
        TrikePoint trikePoint,
        double walkToTrikeMeters,
        double rideDistanceMeters,
        int? routeSampleIndex = null,
        RouteAnchor? fullRouteAnchor = null)
    {
        var walkTime =
            walkToTrikeMeters /
            WalkingSpeedMetersPerSecond;

        var rideTime =
            rideDistanceMeters /
            TrikeSpeedMetersPerSecond;

        var fare =
            ComputeTrikeFarePesos(rideDistanceMeters);

        return new AccessCandidate(
            AccessMode.Trike,
            anchor,
            walkToTrikeMeters,
            walkTime,
            trikePoint,
            rideDistanceMeters,
            rideTime,
            fare,
            ValueOfTimePesosPerMinute,
            WalkingFatiguePesosPerKilometer,
            routeSampleIndex,
            fullRouteAnchor);
    }

    private double ComputeTrikeFarePesos(
        double distanceMeters)
    {
        if (distanceMeters <= TrikeBaseDistanceMeters)
            return TrikeBaseFarePesos;

        var extraKilometers =
            Math.Ceiling(
                (distanceMeters - TrikeBaseDistanceMeters) /
                1_000);

        return TrikeBaseFarePesos +
               extraKilometers *
               TrikePerAdditionalKmPesos;
    }

    internal double GeneralizedCostFromTimeAndFare(
        double timeSeconds,
        double farePesos) =>
        farePesos +
        timeSeconds / 60.0 *
        ValueOfTimePesosPerMinute;

    internal double GeneralizedCostFromWalking(
        double timeSeconds,
        double distanceMeters) =>
        GeneralizedCostFromTimeAndFare(timeSeconds, 0) +
        distanceMeters / 1_000 * WalkingFatiguePesosPerKilometer;

    /// <summary>
    /// prefix[i] is a bounded, deterministic set of useful boarding
    /// occurrences strictly before i. A scalar provisional minimum is not
    /// sufficient here: straight-line access can make a downstream occurrence
    /// look cheaper before Valhalla confirms the road route, and replacing the
    /// prefix with that one occurrence makes transfer search disagree with the
    /// diverse boarding states retained by direct search.
    /// </summary>
    private IReadOnlyList<AccessCandidate>[] ComputePrefixAccessOptions(
        string routeId,
        AccessCandidate?[] access,
        IEnumerable<AccessCandidate>? authoritative = null)
    {
        var prefixes = new IReadOnlyList<AccessCandidate>[access.Length];
        var available = new List<AccessCandidate>();
        var authoritativeList = DistinctAccessOccurrences(authoritative ?? []);

        for (var i = 0; i < access.Length; i++)
        {
            var downstreamProgress = _routeSearchAnchors[routeId][i]
                .DistanceFromRouteStartMeters;
            var preferred = authoritativeList
                .Where(candidate => GetAccessProgressMeters(candidate) < downstreamProgress)
                .ToList();
            var eligible = available
                .Where(candidate => GetAccessProgressMeters(candidate) < downstreamProgress)
                .ToList();
            prefixes[i] = SelectUsefulAccessStates(eligible, preferred);
            if (access[i] is { } option)
            {
                var duplicateIndex = available.FindIndex(existing =>
                    IsSameAccessOccurrence(existing, option));
                if (duplicateIndex < 0)
                    available.Add(option);
                else if (option.GeneralizedCostPesos <
                         available[duplicateIndex].GeneralizedCostPesos)
                    available[duplicateIndex] = option;
            }
        }

        return prefixes;
    }

    private IReadOnlyList<AccessCandidate> SelectUsefulAccessStates(
        IReadOnlyList<AccessCandidate> available,
        IReadOnlyList<AccessCandidate> authoritative)
    {
        // Search branching multiplies access occurrences by route states, so
        // both board-prefix and destination-completion callers use the same
        // configured per-route cap. Objective representatives are
        // deterministic and remaining capacity is filled by maximum progress
        // separation, never enumeration order.
        var prefixLimit = MaxBoardingVariantsPerRoute;

        var distinct = available.OrderBy(GetAccessProgressMeters).ToList();
        var selected = new List<AccessCandidate>();

        foreach (var candidate in authoritative)
            Add(candidate);

        if (selected.Count >= prefixLimit)
            return selected;

        if (distinct.Count <= prefixLimit - selected.Count)
        {
            foreach (var candidate in distinct)
                Add(candidate);
            return selected;
        }

        Add(distinct.OrderBy(GetAccessProgressMeters)
            .ThenBy(candidate => candidate.GeneralizedCostPesos).First());
        Add(distinct.OrderBy(candidate => candidate.GeneralizedCostPesos)
            .ThenBy(GetAccessProgressMeters).First());
        Add(distinct.OrderBy(candidate => candidate.TotalTimeSeconds)
            .ThenBy(candidate => candidate.GeneralizedCostPesos).First());
        Add(distinct.OrderBy(candidate => candidate.FarePesos)
            .ThenBy(candidate => candidate.GeneralizedCostPesos).First());
        Add(distinct.OrderBy(MinimumAccessWalkMeters)
            .ThenBy(candidate => candidate.GeneralizedCostPesos).First());
        Add(distinct.OrderBy(MinimumAccessTrikeMeters)
            .ThenBy(candidate => candidate.GeneralizedCostPesos).First());

        while (selected.Count < prefixLimit && selected.Count < distinct.Count)
        {
            var before = selected.Count;
            Add(distinct
                .Where(candidate => !selected.Any(existing =>
                    IsSameAccessOccurrence(existing, candidate)))
                .OrderByDescending(candidate => selected.Min(chosen =>
                    Math.Abs(GetAccessProgressMeters(candidate) -
                        GetAccessProgressMeters(chosen))))
                .ThenBy(candidate => candidate.GeneralizedCostPesos)
                .First());
            if (selected.Count == before)
                break;
        }

        return selected;

        void Add(AccessCandidate candidate)
        {
            if (selected.Count >= prefixLimit)
                return;
            if (!selected.Any(existing => IsSameAccessOccurrence(existing, candidate)))
                selected.Add(candidate);
        }
    }

    private static List<AccessCandidate> DistinctAccessOccurrences(
        IEnumerable<AccessCandidate> candidates)
    {
        var distinct = new List<AccessCandidate>();
        foreach (var candidate in candidates
                     .OrderBy(candidate => candidate.GeneralizedCostPesos)
                     .ThenBy(GetAccessProgressMeters)
                     .ThenBy(candidate => candidate.Anchor.Latitude)
                     .ThenBy(candidate => candidate.Anchor.Longitude))
        {
            if (!distinct.Any(existing => IsSameAccessOccurrence(existing, candidate)))
                distinct.Add(candidate);
        }

        return distinct.OrderBy(GetAccessProgressMeters).ToList();
    }

    private static bool IsSameAccessOccurrence(
        AccessCandidate left,
        AccessCandidate right) =>
        ApproximateDistanceMeters(
            left.Anchor.Latitude,
            left.Anchor.Longitude,
            right.Anchor.Latitude,
            right.Anchor.Longitude) <= 1.0 &&
        Math.Abs(GetAccessProgressMeters(left) - GetAccessProgressMeters(right)) <= 1.0;

    private static string AccessOccurrenceKey(AccessCandidate candidate) => string.Join(':',
        Math.Round(candidate.Anchor.Latitude, 6),
        Math.Round(candidate.Anchor.Longitude, 6),
        Math.Round(candidate.FullRouteAnchor?.DistanceFromRouteStartMeters ?? -1, 1));

    private static double GetAccessProgressMeters(AccessCandidate candidate) =>
        candidate.FullRouteAnchor?.DistanceFromRouteStartMeters ??
        candidate.RouteSampleIndex ?? double.PositiveInfinity;

    private static double MinimumAccessWalkMeters(AccessCandidate candidate) =>
        candidate.AllAlternatives.Min(alternative => alternative.WalkDistanceMeters);

    private static double MinimumAccessTrikeMeters(AccessCandidate candidate) =>
        candidate.AllAlternatives
            .Where(alternative => alternative.Mode == AccessMode.Trike)
            .Select(alternative => alternative.TrikeRideDistanceMeters ?? double.PositiveInfinity)
            .DefaultIfEmpty(double.PositiveInfinity)
            .Min();

    // -------------------------------------------------------------------
    // Interchange graph
    // -------------------------------------------------------------------

    /// <summary>
    /// Builds multiple useful interchange edges between each pair of routes.
    ///
    /// The old implementation kept only the single globally closest sample
    /// pair. That can hide a much more useful transfer farther along the
    /// routes. We therefore keep up to MaxInterchangesPerRoutePair
    /// geographically distinct pairs within MaxTransferWalkMeters.
    /// </summary>
}
