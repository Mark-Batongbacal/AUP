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
                                await _valhallaService.GetMatrixAsync(
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
                    transfers.Any(t => t is null))
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

                var totalTime =
                    origin.TotalTimeSeconds +
                    destination.TotalTimeSeconds +
                    transferTimes.Sum() +
                    EstimateJeepneyTravelTimeSeconds(candidate.Legs);

                var totalFare =
                    origin.TotalFarePesos +
                    destination.TotalFarePesos +
                    candidate.Legs.Count * JeepneyBaseFarePesos;

                var totalCost =
                    origin.GeneralizedCostPesos +
                    destination.GeneralizedCostPesos +
                    transferTimes.Sum(time =>
                        GeneralizedCostFromTimeAndFare(
                            time,
                            0)) +
                    GeneralizedCostFromTimeAndFare(
                        EstimateJeepneyTravelTimeSeconds(candidate.Legs),
                        candidate.Legs.Count * JeepneyBaseFarePesos);

                var routeLegs = BuildCompleteLegs(
                    candidate,
                    origin,
                    destination,
                    transfers.Select(transfer => transfer!.Value).ToList(),
                    (originLatitude, originLongitude),
                    (destinationLatitude, destinationLongitude));

                return new JeepneyTripPlan
                {
                    Legs = routeLegs,

                    OriginAccess = origin,
                    DestinationAccess = destination,

                    TransferWalkDistancesMeters =
                        transferDistances,

                    TransferWalkTimesSeconds =
                        transferTimes,

                    TotalTimeSeconds = totalTime,
                    TotalFarePesos = totalFare,
                    GeneralizedCostPesos = totalCost
                };
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

            var boardIndex = GetNearestSampleIndex(samples, leg.Board);
            var alightIndex = GetNearestSampleIndex(samples, leg.Alight);

            time += JeepneyBoardingWaitTimeSeconds +
                RouteDistanceBetweenSamples(samples, boardIndex, alightIndex) /
                JeepneySpeedMetersPerSecond;
        }

        return time;
    }

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
            return [BuildWalkLeg(
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
        var boardIndex = GetNearestSampleIndex(samples, leg.Board);
        var alightIndex = GetNearestSampleIndex(samples, leg.Alight);
        var distance = RouteDistanceBetweenSamples(samples, boardIndex, alightIndex);
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

    private static JeepneyTripLeg BuildWalkLeg(
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
            GeneralizedCostPesos = GeneralizedCostFromTimeAndFare(time, 0),
            WalkDistanceMeters = distance,
            WalkTimeSeconds = time
        };

    // -------------------------------------------------------------------
    // Trike-aware access
    // -------------------------------------------------------------------

    private async Task<JeepneyAccessSegment?>
        ConfirmAccessAsync(
            AccessCandidate candidate,
            (double Latitude, double Longitude) walkAnchorPoint,
            (double Latitude, double Longitude) rideTargetPoint,
        CancellationToken cancellationToken)
    {
        if (candidate.Mode == AccessMode.Walk)
        {
            return await ConfirmWalkingAccessAsync(
                walkAnchorPoint,
                rideTargetPoint,
                null,
                cancellationToken);
        }

        var trikePoint = candidate.TrikePoint!;

        var walkTask =
            _valhallaService.GetMatrixAsync(
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
            _valhallaService.GetMatrixAsync(
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
        {
            // A geometric trike candidate is not proof of road-network
            // reachability. Recover only with an explicitly walking segment.
            return await ConfirmWalkingAccessAsync(
                walkAnchorPoint,
                rideTargetPoint,
                MaxWalkAccessDistanceMeters,
                cancellationToken);
        }

        var walkDistance =
            walkResult.Distance!.Value * 1_000;

        var walkTime = walkResult.Time!.Value;

        var rideDistance =
            rideResult.Distance!.Value * 1_000;

        var rideTime = rideResult.Time!.Value;

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
                    fare)
        };
    }

    private async Task<JeepneyAccessSegment?> ConfirmWalkingAccessAsync(
        (double Latitude, double Longitude) from,
        (double Latitude, double Longitude) to,
        double? maximumDistanceMeters,
        CancellationToken cancellationToken)
    {
        var results = await _valhallaService.GetMatrixAsync(
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
            GeneralizedCostPesos = GeneralizedCostFromTimeAndFare(time, 0)
        };
    }

    private AccessCandidate[] ComputeBoardAccessOptions(
        List<(double Latitude, double Longitude)> samples,
        double originLatitude,
        double originLongitude)
    {
        _logger.LogWarning(
            "TRIKE DEBUG: ENTERED ComputeBoardAccessOptions. Origin={Lat},{Lon}, Samples={Count}",
            originLatitude,
            originLongitude,
            samples.Count);
        var trikeCandidates =
            FindNearbyTrikePoints(
                originLatitude,
                originLongitude);
        _logger.LogWarning(
    "TRIKE DEBUG: Found {Count} trike candidates",
    trikeCandidates.Count);

        var options =
            new AccessCandidate[samples.Count];

        for (var i = 0; i < samples.Count; i++)
        {
            var anchor = samples[i];

            var directDistance =
                ApproximateDistanceMeters(
                    originLatitude,
                    originLongitude,
                    anchor.Latitude,
                    anchor.Longitude);

            var best =
                WalkAccess(anchor, directDistance);

            // Trike points are candidates only. The geometric ranking here is
            // deliberately cheap; the selected option is confirmed through
            // real Valhalla walking + road routing later.
            foreach (var candidate in trikeCandidates)
            {
                var walkToTrikeMeters =
                    ApproximateDistanceMeters(
                        originLatitude,
                        originLongitude,
                        candidate.Latitude,
                        candidate.Longitude);

                var rideDistance =
                    ApproximateDistanceMeters(
                        candidate.Latitude,
                        candidate.Longitude,
                        anchor.Latitude,
                        anchor.Longitude);

                var trikeOption =
                    TrikeAccess(
                        anchor,
                        candidate,
                        walkToTrikeMeters,
                        rideDistance);

                

                if (trikeOption.GeneralizedCostPesos <
                    best.GeneralizedCostPesos)
                {
                    best = trikeOption;
                }
            }

            options[i] = best;
        }

        return options;
    }

    private AccessCandidate[] ComputeAlightAccessOptions(
        List<(double Latitude, double Longitude)> samples,
        double destinationLatitude,
        double destinationLongitude)
    {
        var options =
            new AccessCandidate[samples.Count];

        for (var i = 0; i < samples.Count; i++)
        {
            var anchor = samples[i];

            var directDistance =
                ApproximateDistanceMeters(
                    anchor.Latitude,
                    anchor.Longitude,
                    destinationLatitude,
                    destinationLongitude);

            var best =
                WalkAccess(anchor, directDistance);

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
                        rideDistance);

                if (trikeOption.GeneralizedCostPesos <
                    best.GeneralizedCostPesos)
                {
                    best = trikeOption;
                }
            }

            options[i] = best;
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

    private static AccessCandidate WalkAccess(
        (double Latitude, double Longitude) anchor,
        double distanceMeters)
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
            null);
    }

    private static AccessCandidate TrikeAccess(
        (double Latitude, double Longitude) anchor,
        TrikePoint trikePoint,
        double walkToTrikeMeters,
        double rideDistanceMeters)
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
            fare);
    }

    private static double ComputeTrikeFarePesos(
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

    private static double GeneralizedCostFromTimeAndFare(
        double timeSeconds,
        double farePesos) =>
        farePesos +
        timeSeconds / 60.0 *
        ValueOfTimePesosPerMinute;

    // prefix[i] = cheapest access strictly before i.
    private static (
        double[] Cost,
        AccessCandidate?[] Access)
        ComputePrefixMinAccess(
            AccessCandidate[] access)
    {
        var cost = new double[access.Length];
        var chosen = new AccessCandidate?[access.Length];

        var bestCost = double.PositiveInfinity;
        AccessCandidate? bestAccess = null;

        for (var i = 0; i < access.Length; i++)
        {
            cost[i] = bestCost;
            chosen[i] = bestAccess;

            if (access[i].GeneralizedCostPesos < bestCost)
            {
                bestCost =
                    access[i].GeneralizedCostPesos;

                bestAccess = access[i];
            }
        }

        return (cost, chosen);
    }

    // suffix[i] = cheapest access strictly after i.
    private static (
        double[] Cost,
        AccessCandidate?[] Access)
        ComputeSuffixMinAccess(
            AccessCandidate[] access)
    {
        var cost = new double[access.Length];
        var chosen = new AccessCandidate?[access.Length];

        var bestCost = double.PositiveInfinity;
        AccessCandidate? bestAccess = null;

        for (var i = access.Length - 1; i >= 0; i--)
        {
            cost[i] = bestCost;
            chosen[i] = bestAccess;

            if (access[i].GeneralizedCostPesos < bestCost)
            {
                bestCost =
                    access[i].GeneralizedCostPesos;

                bestAccess = access[i];
            }
        }

        return (cost, chosen);
    }

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
