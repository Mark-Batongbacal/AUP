using System.Text.Json;
using backend.Models.Routing;
using backend.Models.Valhalla;
using Microsoft.Extensions.Logging;

namespace backend.Services.Route;

public class JeepneyRoutingService : IJeepneyRoutingService
{
    private const int MaxNearbyRoutes = 20;
    private const int MaxTripOptions = 10;

    // Route geometry is sampled by geographic distance rather than coordinate
    // index so dense source vertices do not consume the sample budget.
    private const double DefaultSampleIntervalMeters = 150.0;
    private const int MaxRouteSamples = 40;

    private const int MatrixChunkSize = 100;

    // Keep several geographically distinct transfer candidates between a pair
    // of routes. One global closest pair is not sufficient because the closest
    // interchange is not necessarily useful for every origin/destination.
    private const int MaxInterchangesPerRoutePair = 4;
    private const double MaxTransferWalkMeters = 400;

    // Trike points are candidate pickup/dropoff points. The geometric nearest
    // point is not necessarily the best walking point, so keep several nearby
    // candidates before selecting one by the cheap generalized-cost estimate.
    private const int MaxNearbyTrikeCandidates = 3;
    private const double MaxWalkToTrikePointMeters = 1000;

    // Provisional local fare model. Verify against the actual municipality/TODA
    // fare rules before treating these values as authoritative.
    private const double TrikeBaseFarePesos = 35;
    private const double TrikeBaseDistanceMeters = 3_000;
    private const double TrikePerAdditionalKmPesos = 15;

    private const double ValueOfTimePesosPerMinute = 2.0;

    // Used only for candidate generation before Valhalla confirmation.
    private const double WalkingSpeedMetersPerSecond = 1.2;
    private const double TrikeSpeedMetersPerSecond = 5.6;

    // Valhalla has no built-in tricycle profile. "auto" is currently only a
    // road-network stand-in; replace with a local/custom trike model later.
    private const string TrikeCostingModel = "auto";

    private const int MaxCandidatesToConfirm = 60;

    private const double EarthRadiusMeters = 6_371_000;

    private readonly IValhallaService _valhallaService;
    private readonly ILogger<JeepneyRoutingService> _logger;
    private readonly List<StaticJeepneyRoute> _routes;
    private readonly List<TrikePoint> _trikePoints;

    private readonly Dictionary<string, List<(double Latitude, double Longitude)>> _routeSamples;
    private readonly Dictionary<string, List<RouteInterchange>> _interchangesByRoute;

    public JeepneyRoutingService(
        IValhallaService valhallaService,
        IWebHostEnvironment environment,
        ILogger<JeepneyRoutingService> logger)
    {
        _valhallaService = valhallaService;
        _logger = logger;

        var routesPath = Path.Combine(
            environment.ContentRootPath,
            "TestData",
            "jeepney-routes.json");

        if (!File.Exists(routesPath))
        {
            throw new FileNotFoundException(
                "Static jeepney route file was not found.",
                routesPath);
        }

        _routes = JsonSerializer.Deserialize<List<StaticJeepneyRoute>>(
            File.ReadAllText(routesPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? [];

        var trikePointsPath = Path.Combine(
            environment.ContentRootPath,
            "TestData",
            "trike-points.json");

        if (File.Exists(trikePointsPath))
        {
            _trikePoints = JsonSerializer.Deserialize<List<TrikePoint>>(
                File.ReadAllText(trikePointsPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? [];
        }
        else
        {
            _logger.LogWarning(
                "Trike points file not found at {Path}; trike-assisted routing will be unavailable.",
                trikePointsPath);

            _trikePoints = [];
        }

        _routeSamples = _routes
            .Where(route => route.Coordinates.Count >= 2)
            .ToDictionary(
                route => route.RouteId,
                route => SampleRoutePoints(route.Coordinates).ToList());

        var routeNamesById = _routes.ToDictionary(
            route => route.RouteId,
            route => route.RouteName);

        _interchangesByRoute = BuildInterchangeGraph(
            _routeSamples,
            routeNamesById);
    }

    // -------------------------------------------------------------------
    // Pickup-only lookup
    // -------------------------------------------------------------------

    public async Task<List<NearbyJeepneyResponse>> FindNearbyRoutesAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<SampledRoutePoint>();

        foreach (var route in _routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_routeSamples.TryGetValue(route.RouteId, out var samples))
                continue;

            foreach (var point in samples)
            {
                var distanceMeters = ApproximateDistanceMeters(
                    latitude,
                    longitude,
                    point.Latitude,
                    point.Longitude);

                candidates.Add(new SampledRoutePoint(
                    route.RouteId,
                    new NearbyJeepneyResponse
                    {
                        RouteId = route.RouteId,
                        RouteName = route.RouteName,
                        RouteDistanceMeters = distanceMeters,
                        NearestPointLatitude = point.Latitude,
                        NearestPointLongitude = point.Longitude
                    }));
            }
        }

        if (candidates.Count == 0)
            return [];

        try
        {
            return await RankByWalkingDistanceAsync(
                candidates,
                latitude,
                longitude,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to fetch Valhalla walking matrix; returning straight-line ranked routes.");

            return candidates
                .GroupBy(candidate => candidate.RouteId)
                .Select(group => group
                    .OrderBy(candidate => candidate.Response.RouteDistanceMeters)
                    .First()
                    .Response)
                .OrderBy(candidate => candidate.RouteDistanceMeters)
                .Take(MaxNearbyRoutes)
                .ToList();
        }
    }

    private async Task<List<NearbyJeepneyResponse>> RankByWalkingDistanceAsync(
        List<SampledRoutePoint> candidates,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        candidates.Sort((a, b) =>
            a.Response.RouteDistanceMeters.CompareTo(
                b.Response.RouteDistanceMeters));

        var routeBestWalking = new Dictionary<string, NearbyJeepneyResponse>();
        var index = 0;

        while (index < candidates.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Straight-line distance is a valid lower bound for walking
            // distance. Therefore this pruning cannot discard a candidate
            // that could beat the current confirmed top-N.
            if (routeBestWalking.Count >= MaxNearbyRoutes)
            {
                var bound = routeBestWalking.Values
                    .Select(response => response.WalkingDistanceMeters)
                    .OrderBy(distance => distance)
                    .ElementAt(MaxNearbyRoutes - 1);

                if (candidates[index].Response.RouteDistanceMeters > bound)
                    break;
            }

            var chunkEnd = Math.Min(
                index + MatrixChunkSize,
                candidates.Count);

            var chunk = candidates.GetRange(
                index,
                chunkEnd - index);

            index = chunkEnd;

            var matrixResults = await _valhallaService.GetMatrixAsync(
                new ValhallaLocation
                {
                    Lat = latitude,
                    Lon = longitude
                },
                chunk.Select(candidate => new ValhallaLocation
                {
                    Lat = candidate.Response.NearestPointLatitude,
                    Lon = candidate.Response.NearestPointLongitude
                }).ToList(),
                "pedestrian",
                cancellationToken);

            foreach (var result in matrixResults)
            {
                if (result.FromIndex != 0 ||
                    result.ToIndex < 0 ||
                    result.ToIndex >= chunk.Count ||
                    result.Distance is null ||
                    result.Time is null)
                {
                    continue;
                }

                var sample = chunk[result.ToIndex];
                var walkingDistanceMeters = result.Distance.Value * 1_000;

                if (routeBestWalking.TryGetValue(
                        sample.RouteId,
                        out var existing) &&
                    existing.WalkingDistanceMeters <= walkingDistanceMeters)
                {
                    continue;
                }

                sample.Response.WalkingDistanceMeters =
                    walkingDistanceMeters;

                sample.Response.WalkingTimeSeconds =
                    result.Time.Value;

                routeBestWalking[sample.RouteId] =
                    sample.Response;
            }
        }

        return routeBestWalking.Values
            .OrderBy(response => response.WalkingDistanceMeters)
            .ThenBy(response => response.WalkingTimeSeconds)
            .Take(MaxNearbyRoutes)
            .ToList();
    }

    // -------------------------------------------------------------------
    // Single-route trip
    // -------------------------------------------------------------------

    public async Task<List<JeepneyTripOption>> FindConnectingRoutesAsync(
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<RouteConnectionCandidate>();

        foreach (var route in _routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_routeSamples.ContainsKey(route.RouteId))
                continue;

            var candidate = FindBestConnection(
                route,
                originLatitude,
                originLongitude,
                destinationLatitude,
                destinationLongitude);

            if (candidate is not null)
                candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return [];

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

                if (board is null || alight is null)
                    return null;

                return new JeepneyTripOption
                {
                    RouteId = candidate.RouteId,
                    RouteName = candidate.RouteName,

                    BoardLatitude =
                        candidate.BoardAccess.Anchor.Latitude,

                    BoardLongitude =
                        candidate.BoardAccess.Anchor.Longitude,

                    BoardAccess = board,

                    AlightLatitude =
                        candidate.AlightAccess.Anchor.Latitude,

                    AlightLongitude =
                        candidate.AlightAccess.Anchor.Longitude,

                    AlightAccess = alight,

                    TotalTimeSeconds =
                        board.TotalTimeSeconds +
                        alight.TotalTimeSeconds,

                    TotalFarePesos =
                        board.TotalFarePesos +
                        alight.TotalFarePesos,

                    GeneralizedCostPesos =
                        board.GeneralizedCostPesos +
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
            .OrderBy(option => option.GeneralizedCostPesos)
            .Take(MaxTripOptions)
            .ToList();
    }

    private RouteConnectionCandidate? FindBestConnection(
        StaticJeepneyRoute route,
        double originLatitude,
        double originLongitude,
        double destinationLatitude,
        double destinationLongitude)
    {
        var samples = _routeSamples[route.RouteId];

        if (samples.Count < 2)
            return null;

        var boardAccessOptions =
            ComputeBoardAccessOptions(
                samples,
                originLatitude,
                originLongitude);

        var alightAccessOptions =
            ComputeAlightAccessOptions(
                samples,
                destinationLatitude,
                destinationLongitude);

        // Prefix[i] intentionally means "strictly before i". This preserves
        // route direction and prevents boarding at or after the alighting point.
        var (boardPrefixCost, boardPrefixAccess) =
            ComputePrefixMinAccess(boardAccessOptions);

        var bestTotal = double.PositiveInfinity;
        AccessCandidate? chosenBoardAccess = null;
        AccessCandidate? chosenAlightAccess = null;

        for (var i = 0; i < samples.Count; i++)
        {
            if (boardPrefixAccess[i] is null)
                continue;

            var total =
                boardPrefixCost[i] +
                alightAccessOptions[i].GeneralizedCostPesos;

            if (total < bestTotal)
            {
                bestTotal = total;
                chosenBoardAccess = boardPrefixAccess[i];
                chosenAlightAccess = alightAccessOptions[i];
            }
        }

        if (chosenBoardAccess is null ||
            chosenAlightAccess is null)
        {
            return null;
        }

        return new RouteConnectionCandidate(
            route.RouteId,
            route.RouteName,
            chosenBoardAccess,
            chosenAlightAccess);
    }

    // -------------------------------------------------------------------
    // Full journey planning
    // -------------------------------------------------------------------

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

            candidates.Add(new JourneyCandidate(
                [
                    new JourneyLegCandidate(
                        direct.RouteId,
                        direct.RouteName,
                        direct.BoardAccess.Anchor,
                        direct.AlightAccess.Anchor)
                ],
                direct.BoardAccess,
                direct.AlightAccess,
                []));
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

                    candidates.Add(new JourneyCandidate(
                        [
                            new JourneyLegCandidate(
                                route.RouteId,
                                route.RouteName,
                                boardAccess.Anchor,
                                transferFromA),

                            new JourneyLegCandidate(
                                edge1.OtherRouteId,
                                edge1.OtherRouteName,
                                transferToB,
                                transferFromB),

                            new JourneyLegCandidate(
                                edge2.OtherRouteId,
                                edge2.OtherRouteName,
                                transferToC,
                                alightAccessC.Anchor)
                        ],
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
                        ]));
                }
            }
        }

        if (candidates.Count == 0)
            return [];

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

        return confirmed
            .OrderBy(plan => plan.GeneralizedCostPesos)
            .Take(MaxTripOptions)
            .ToList();
    }

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

        const double averageJeepneySpeedMetersPerSecond = 6.5;

        var rideTime =
            (firstRideMeters + secondRideMeters) /
            averageJeepneySpeedMetersPerSecond;

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
                rideTime,
                0);

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
                    transferTimes.Sum();

                var totalFare =
                    origin.TotalFarePesos +
                    destination.TotalFarePesos;

                var totalCost =
                    origin.GeneralizedCostPesos +
                    destination.GeneralizedCostPesos +
                    transferTimes.Sum(time =>
                        GeneralizedCostFromTimeAndFare(
                            time,
                            0));

                return new JeepneyTripPlan
                {
                    Legs = candidate.Legs
                        .Select(ToTripLeg)
                        .ToList(),

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

    private static JeepneyTripLeg ToTripLeg(
        JourneyLegCandidate leg) =>
        new()
        {
            RouteId = leg.RouteId,
            RouteName = leg.RouteName,
            BoardLatitude = leg.Board.Latitude,
            BoardLongitude = leg.Board.Longitude,
            AlightLatitude = leg.Alight.Latitude,
            AlightLongitude = leg.Alight.Longitude
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
            var results =
                await _valhallaService.GetMatrixAsync(
                    new ValhallaLocation
                    {
                        Lat = walkAnchorPoint.Latitude,
                        Lon = walkAnchorPoint.Longitude
                    },
                    [
                        new ValhallaLocation
                        {
                            Lat = rideTargetPoint.Latitude,
                            Lon = rideTargetPoint.Longitude
                        }
                    ],
                    "pedestrian",
                    cancellationToken);

            var result = results.FirstOrDefault(r =>
                r.FromIndex == 0 &&
                r.ToIndex == 0 &&
                r.Distance is not null &&
                r.Time is not null);

            if (result is null)
                return null;

            var distance = result.Distance!.Value * 1_000;
            var time = result.Time!.Value;

            return new JeepneyAccessSegment
            {
                Mode = AccessMode.Walk,
                WalkDistanceMeters = distance,
                WalkTimeSeconds = time,
                TotalTimeSeconds = time,
                TotalFarePesos = 0,
                GeneralizedCostPesos =
                    GeneralizedCostFromTimeAndFare(time, 0)
            };
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
            return null;

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
    private static Dictionary<string, List<RouteInterchange>>
        BuildInterchangeGraph(
            Dictionary<string,
                List<(double Latitude, double Longitude)>> routeSamples,
            Dictionary<string, string> routeNamesById)
    {
        var edgesByRoute =
            new Dictionary<string, List<RouteInterchange>>();

        var routeIds =
            routeSamples.Keys.ToList();

        void AddEdge(
            string ownId,
            int ownIndex,
            string otherId,
            string otherName,
            int otherIndex,
            double distance)
        {
            if (!edgesByRoute.TryGetValue(
                    ownId,
                    out var list))
            {
                list = [];
                edgesByRoute[ownId] = list;
            }

            list.Add(
                new RouteInterchange(
                    ownIndex,
                    otherId,
                    otherName,
                    otherIndex,
                    distance));
        }

        for (var i = 0; i < routeIds.Count; i++)
        {
            for (var j = i + 1; j < routeIds.Count; j++)
            {
                var routeA = routeIds[i];
                var routeB = routeIds[j];

                var samplesA = routeSamples[routeA];
                var samplesB = routeSamples[routeB];

                var pairCandidates =
                    new List<InterchangePairCandidate>();

                for (var a = 0; a < samplesA.Count; a++)
                {
                    for (var b = 0; b < samplesB.Count; b++)
                    {
                        var distance =
                            ApproximateDistanceMeters(
                                samplesA[a].Latitude,
                                samplesA[a].Longitude,
                                samplesB[b].Latitude,
                                samplesB[b].Longitude);

                        if (distance <= MaxTransferWalkMeters)
                        {
                            pairCandidates.Add(
                                new InterchangePairCandidate(
                                    a,
                                    b,
                                    distance));
                        }
                    }
                }

                if (pairCandidates.Count == 0)
                    continue;

                // Greedily select the closest candidate, then suppress
                // candidates that are effectively the same interchange.
                // This gives several spatially distinct transfer areas
                // instead of four nearly identical neighboring sample pairs.
                pairCandidates.Sort(
                    (x, y) =>
                        x.DistanceMeters.CompareTo(
                            y.DistanceMeters));

                var selected =
                    new List<InterchangePairCandidate>();

                foreach (var candidate in pairCandidates)
                {
                    var tooSimilar =
                        selected.Any(existing =>
                            IsSameInterchangeArea(
                                candidate,
                                existing));

                    if (tooSimilar)
                        continue;

                    selected.Add(candidate);

                    if (selected.Count >=
                        MaxInterchangesPerRoutePair)
                    {
                        break;
                    }
                }

                var nameA =
                    routeNamesById[routeA];

                var nameB =
                    routeNamesById[routeB];

                foreach (var candidate in selected)
                {
                    AddEdge(
                        routeA,
                        candidate.IndexA,
                        routeB,
                        nameB,
                        candidate.IndexB,
                        candidate.DistanceMeters);

                    AddEdge(
                        routeB,
                        candidate.IndexB,
                        routeA,
                        nameA,
                        candidate.IndexA,
                        candidate.DistanceMeters);
                }
            }
        }

        return edgesByRoute;
    }

    private static bool IsSameInterchangeArea(
        InterchangePairCandidate a,
        InterchangePairCandidate b)
    {
        // The sample spacing is roughly 150m, so requiring a difference of at
        // least two sample positions prevents multiple adjacent samples from
        // representing the same physical transfer area.
        const int minSampleIndexSeparation = 2;

        return Math.Abs(a.IndexA - b.IndexA) <
                   minSampleIndexSeparation &&
               Math.Abs(a.IndexB - b.IndexB) <
                   minSampleIndexSeparation;
    }

    // -------------------------------------------------------------------
    // Route sampling
    // -------------------------------------------------------------------

    private static IEnumerable<(
        double Latitude,
        double Longitude)>
        SampleRoutePoints(
            IReadOnlyList<double[]> routeCoordinates,
            double sampleIntervalMeters =
                DefaultSampleIntervalMeters,
            int maxSamples = MaxRouteSamples)
    {
        if (routeCoordinates.Count == 0)
            yield break;

        var points =
            new (double Lat, double Lon)[
                routeCoordinates.Count];

        for (var i = 0;
             i < routeCoordinates.Count;
             i++)
        {
            // GeoJSON order: [longitude, latitude].
            points[i] =
                (routeCoordinates[i][1],
                 routeCoordinates[i][0]);
        }

        if (points.Length == 1)
        {
            yield return points[0];
            yield break;
        }

        var segmentLengths =
            new double[points.Length - 1];

        var totalLength = 0.0;

        for (var i = 0;
             i < points.Length - 1;
             i++)
        {
            var length =
                ApproximateDistanceMeters(
                    points[i].Lat,
                    points[i].Lon,
                    points[i + 1].Lat,
                    points[i + 1].Lon);

            segmentLengths[i] = length;
            totalLength += length;
        }

        if (totalLength <= 0)
        {
            yield return points[0];
            yield break;
        }

        var effectiveInterval =
            sampleIntervalMeters;

        var estimatedSampleCount =
            totalLength /
            effectiveInterval + 1;

        if (estimatedSampleCount > maxSamples &&
            maxSamples > 1)
        {
            effectiveInterval =
                totalLength /
                (maxSamples - 1);
        }

        yield return points[0];

        var samplesEmitted = 1;
        var distanceSinceLastSample = 0.0;

        for (var i = 0;
             i < segmentLengths.Length &&
             samplesEmitted < maxSamples;
             i++)
        {
            var segmentStart = points[i];
            var segmentEnd = points[i + 1];
            var segmentLength = segmentLengths[i];

            if (segmentLength <= 0)
                continue;

            var traveledInSegment = 0.0;

            while (
                samplesEmitted < maxSamples &&
                distanceSinceLastSample +
                    (segmentLength - traveledInSegment) >=
                    effectiveInterval)
            {
                var neededInSegment =
                    effectiveInterval -
                    distanceSinceLastSample;

                traveledInSegment +=
                    neededInSegment;

                var t =
                    traveledInSegment /
                    segmentLength;

                var lat =
                    segmentStart.Lat +
                    (segmentEnd.Lat -
                     segmentStart.Lat) * t;

                var lon =
                    segmentStart.Lon +
                    (segmentEnd.Lon -
                     segmentStart.Lon) * t;

                yield return (lat, lon);

                samplesEmitted++;
                distanceSinceLastSample = 0.0;
            }

            distanceSinceLastSample +=
                segmentLength -
                traveledInSegment;
        }

        // Always preserve the actual route endpoint when possible.
        if (samplesEmitted < maxSamples)
        {
            yield return points[^1];
        }
    }

    // -------------------------------------------------------------------
    // Internal models
    // -------------------------------------------------------------------

    private sealed record SampledRoutePoint(
        string RouteId,
        NearbyJeepneyResponse Response);

    private sealed record RouteConnectionCandidate(
        string RouteId,
        string RouteName,
        AccessCandidate BoardAccess,
        AccessCandidate AlightAccess)
    {
        public double TotalGeneralizedCostPesos =>
            BoardAccess.GeneralizedCostPesos +
            AlightAccess.GeneralizedCostPesos;
    }

    private sealed record InterchangePairCandidate(
        int IndexA,
        int IndexB,
        double DistanceMeters);

    private sealed record RouteInterchange(
        int OwnIndex,
        string OtherRouteId,
        string OtherRouteName,
        int OtherIndex,
        double DistanceMeters);

    private sealed record JourneyLegCandidate(
        string RouteId,
        string RouteName,
        (double Latitude, double Longitude) Board,
        (double Latitude, double Longitude) Alight);

    private sealed record WalkSegmentCandidate(
        (double Latitude, double Longitude) From,
        (double Latitude, double Longitude) To,
        double StraightLineMeters);

    private sealed record JourneyCandidate(
        List<JourneyLegCandidate> Legs,
        AccessCandidate OriginAccess,
        AccessCandidate DestinationAccess,
        List<WalkSegmentCandidate> TransferWalkSegments,
        double? ProvisionalJourneyCostPesos = null)
    {
        public double TotalGeneralizedCostPesos =>
            ProvisionalJourneyCostPesos ??
            (
                OriginAccess.GeneralizedCostPesos +
                DestinationAccess.GeneralizedCostPesos +
                TransferWalkSegments.Sum(segment =>
                    GeneralizedCostFromTimeAndFare(
                        segment.StraightLineMeters /
                        WalkingSpeedMetersPerSecond,
                        0))
            );

        public int TransferCount =>
            Legs.Count - 1;
    }

    private sealed record AccessCandidate(
        AccessMode Mode,
        (double Latitude, double Longitude) Anchor,
        double WalkDistanceMeters,
        double WalkTimeSeconds,
        TrikePoint? TrikePoint,
        double? TrikeRideDistanceMeters,
        double? TrikeRideTimeSeconds,
        double? TrikeFarePesos)
    {
        public double TotalTimeSeconds =>
            WalkTimeSeconds +
            (TrikeRideTimeSeconds ?? 0);

        public double FarePesos =>
            TrikeFarePesos ?? 0;

        public double GeneralizedCostPesos =>
            GeneralizedCostFromTimeAndFare(
                TotalTimeSeconds,
                FarePesos);
    }

    private static double ApproximateDistanceMeters(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        var lat1Rad =
            lat1 * Math.PI / 180;

        var lat2Rad =
            lat2 * Math.PI / 180;

        var deltaLat =
            (lat2 - lat1) * Math.PI / 180;

        var deltaLon =
            (lon2 - lon1) * Math.PI / 180;

        var a =
            Math.Sin(deltaLat / 2) *
            Math.Sin(deltaLat / 2) +
            Math.Cos(lat1Rad) *
            Math.Cos(lat2Rad) *
            Math.Sin(deltaLon / 2) *
            Math.Sin(deltaLon / 2);

        var c =
            2 * Math.Atan2(
                Math.Sqrt(a),
                Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }
}

/// <summary>
/// A fixed tricycle terminal loaded from TestData/trike-points.json.
/// </summary>
public sealed record TrikePoint(
    string Id,
    string Name,
    double Latitude,
    double Longitude);

public enum AccessMode
{
    Walk,
    Trike
}

public sealed class JeepneyAccessSegment
{
    public required AccessMode Mode { get; init; }
    public double WalkDistanceMeters { get; init; }
    public double WalkTimeSeconds { get; init; }
    public string? TrikePointId { get; init; }
    public string? TrikePointName { get; init; }
    public double? TrikeRideDistanceMeters { get; init; }
    public double? TrikeRideTimeSeconds { get; init; }
    public double TotalTimeSeconds { get; init; }
    public double TotalFarePesos { get; init; }
    public double GeneralizedCostPesos { get; init; }
}

public sealed class JeepneyTripOption
{
    public required string RouteId { get; init; }
    public required string RouteName { get; init; }

    public double BoardLatitude { get; init; }
    public double BoardLongitude { get; init; }
    public required JeepneyAccessSegment BoardAccess { get; init; }

    public double AlightLatitude { get; init; }
    public double AlightLongitude { get; init; }
    public required JeepneyAccessSegment AlightAccess { get; init; }

    public double TotalTimeSeconds { get; init; }
    public double TotalFarePesos { get; init; }
    public double GeneralizedCostPesos { get; init; }
}

public sealed class JeepneyTripLeg
{
    public required string RouteId { get; init; }
    public required string RouteName { get; init; }
    public double BoardLatitude { get; init; }
    public double BoardLongitude { get; init; }
    public double AlightLatitude { get; init; }
    public double AlightLongitude { get; init; }
}

public sealed class JeepneyTripPlan
{
    public List<JeepneyTripLeg> Legs { get; init; } = [];
    public required JeepneyAccessSegment OriginAccess { get; init; }
    public required JeepneyAccessSegment DestinationAccess { get; init; }
    public List<double> TransferWalkDistancesMeters { get; init; } = [];
    public List<double> TransferWalkTimesSeconds { get; init; } = [];
    public double TotalTimeSeconds { get; set; }
    public double TotalFarePesos { get; set; }
    public double GeneralizedCostPesos { get; set; }

    public int TransferCount =>
        Legs.Count - 1;
}
