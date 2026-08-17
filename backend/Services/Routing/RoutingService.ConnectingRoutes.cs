using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
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
                        alight.TotalFarePesos +
                        JeepneyBaseFarePesos,

                    GeneralizedCostPesos =
                        board.GeneralizedCostPesos +
                        alight.GeneralizedCostPesos +
                        JeepneyBaseFarePesos
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

            var boardIndex = GetNearestSampleIndex(
                samples,
                boardPrefixAccess[i]!.Anchor);

            var jeepneyTime =
                JeepneyBoardingWaitTimeSeconds +
                RouteDistanceBetweenSamples(samples, boardIndex, i) /
                JeepneySpeedMetersPerSecond;

            var total =
                boardPrefixCost[i] +
                alightAccessOptions[i].GeneralizedCostPesos +
                GeneralizedCostFromTimeAndFare(
                    jeepneyTime,
                    JeepneyBaseFarePesos);

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

}
