using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
    private Dictionary<string, List<RouteInterchange>>
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

            AddSelfInterchanges(routeIds[i]);
        }

        return edgesByRoute;

        void AddSelfInterchanges(string routeId)
        {
            var samples = routeSamples[routeId];
            var anchors = _routeSearchAnchors[routeId];
            var candidates = new List<InterchangePairCandidate>();

            // Both resulting jeepney legs need positive forward progress, so
            // endpoint pairs cannot produce a usable self-transfer and must
            // not consume the per-route interchange cap.
            for (var fromIndex = 1; fromIndex < samples.Count - 1; fromIndex++)
            {
                for (var toIndex = fromIndex + 1;
                     toIndex < samples.Count - 1;
                     toIndex++)
                {
                    var skippedRouteMeters = anchors[toIndex]
                        .DistanceFromRouteStartMeters - anchors[fromIndex]
                        .DistanceFromRouteStartMeters;
                    if (skippedRouteMeters < MinimumSelfTransferProgressMeters)
                        continue;

                    var shortcutMeters = ApproximateDistanceMeters(
                        samples[fromIndex].Latitude,
                        samples[fromIndex].Longitude,
                        samples[toIndex].Latitude,
                        samples[toIndex].Longitude);
                    if (shortcutMeters > MaxTransferWalkMeters ||
                        skippedRouteMeters < shortcutMeters *
                            MinimumSelfTransferRouteToWalkRatio)
                    {
                        continue;
                    }

                    candidates.Add(new InterchangePairCandidate(
                        fromIndex, toIndex, shortcutMeters));
                }
            }

            candidates.Sort((left, right) =>
                left.DistanceMeters.CompareTo(right.DistanceMeters));
            var selected = new List<InterchangePairCandidate>();
            foreach (var candidate in candidates)
            {
                if (selected.Any(existing =>
                        IsSameInterchangeArea(candidate, existing)))
                {
                    continue;
                }

                selected.Add(candidate);
                if (selected.Count >= MaxInterchangesPerRoutePair)
                    break;
            }

            foreach (var candidate in selected)
            {
                // A self-interchange is intentionally directed. It models
                // leaving one vehicle and boarding another farther forward on
                // the same service, never backward travel on either vehicle.
                AddEdge(
                    routeId,
                    candidate.IndexA,
                    routeId,
                    routeNamesById[routeId],
                    candidate.IndexB,
                    candidate.DistanceMeters);
            }
        }
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
            double sampleIntervalMeters,
            int maxSamples)
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
    // Authoritative full-geometry route progress
    // -------------------------------------------------------------------

    private static FullRouteGeometry BuildFullRouteGeometry(
        IReadOnlyList<double[]> coordinates)
    {
        var points = coordinates
            .Select(point => (Latitude: point[1], Longitude: point[0]))
            .ToList();
        var cumulative = new double[points.Count];

        for (var index = 1; index < points.Count; index++)
        {
            cumulative[index] = cumulative[index - 1] + ApproximateDistanceMeters(
                points[index - 1].Latitude,
                points[index - 1].Longitude,
                points[index].Latitude,
                points[index].Longitude);
        }

        return new FullRouteGeometry(points, cumulative);
    }

    private List<RouteAnchor> BuildSearchAnchors(
        string routeId,
        IReadOnlyList<(double Latitude, double Longitude)> samples)
    {
        var anchors = new List<RouteAnchor>(samples.Count);
        var minimumSegmentIndex = 0;

        foreach (var sample in samples)
        {
            var anchor = ProjectOntoFullRoute(routeId, sample, minimumSegmentIndex);
            anchors.Add(anchor);
            minimumSegmentIndex = anchor.SegmentIndex;
        }

        return anchors;
    }

    private RouteAnchor GetRouteAnchor(
        string routeId,
        int? sampleIndex,
        (double Latitude, double Longitude) point) =>
        sampleIndex is { } index &&
        _routeSearchAnchors.TryGetValue(routeId, out var anchors) &&
        index >= 0 && index < anchors.Count
            ? anchors[index]
            : ProjectOntoFullRoute(routeId, point, 0);

    private RouteAnchor ProjectOntoFullRoute(
        string routeId,
        (double Latitude, double Longitude) point,
        int minimumSegmentIndex,
        int? maximumSegmentIndex = null)
    {
        var geometry = _routeGeometries[routeId];
        var bestDistance = double.PositiveInfinity;
        RouteAnchor? best = null;

        for (var segmentIndex = minimumSegmentIndex;
             segmentIndex < Math.Min(
                 geometry.Points.Count - 1,
                 maximumSegmentIndex ?? geometry.Points.Count - 1);
             segmentIndex++)
        {
            var from = geometry.Points[segmentIndex];
            var to = geometry.Points[segmentIndex + 1];
            var referenceLatitude = (from.Latitude + to.Latitude) / 2;
            var longitudeScale = 111_000 * Math.Cos(referenceLatitude * Math.PI / 180);
            var x = (point.Longitude - from.Longitude) * longitudeScale;
            var y = (point.Latitude - from.Latitude) * 111_000;
            var segmentX = (to.Longitude - from.Longitude) * longitudeScale;
            var segmentY = (to.Latitude - from.Latitude) * 111_000;
            var segmentSquared = segmentX * segmentX + segmentY * segmentY;
            var fraction = segmentSquared <= 0 ? 0 : Math.Clamp(
                (x * segmentX + y * segmentY) / segmentSquared,
                0,
                1);
            var latitude = from.Latitude + (to.Latitude - from.Latitude) * fraction;
            var longitude = from.Longitude + (to.Longitude - from.Longitude) * fraction;
            var distance = ApproximateDistanceMeters(
                point.Latitude, point.Longitude, latitude, longitude);

            if (distance >= bestDistance)
                continue;

            var segmentLength = geometry.CumulativeMeters[segmentIndex + 1] -
                geometry.CumulativeMeters[segmentIndex];
            bestDistance = distance;
            best = new RouteAnchor(
                routeId,
                segmentIndex,
                fraction,
                latitude,
                longitude,
                geometry.CumulativeMeters[segmentIndex] + segmentLength * fraction);
        }

        return best ?? throw new InvalidOperationException(
            $"Route {routeId} has no usable full-geometry segment.");
    }

    private RouteAnchor ProjectOntoSearchRegion(
        string routeId,
        int sampleIndex,
        (double Latitude, double Longitude) point)
    {
        var anchors = _routeSearchAnchors[routeId];
        var geometry = _routeGeometries[routeId];
        var minimumSegment = sampleIndex == 0
            ? 0
            : anchors[sampleIndex - 1].SegmentIndex;
        var maximumSegment = sampleIndex == anchors.Count - 1
            ? geometry.Points.Count - 1
            : anchors[sampleIndex + 1].SegmentIndex + 1;

        return ProjectOntoFullRoute(
            routeId,
            point,
            minimumSegment,
            maximumSegment);
    }

    private static double RouteDistanceBetweenAnchors(
        RouteAnchor from,
        RouteAnchor to) =>
        Math.Max(0, to.DistanceFromRouteStartMeters - from.DistanceFromRouteStartMeters);

    // -------------------------------------------------------------------
    // Internal models
    // -------------------------------------------------------------------

    private sealed record SampledRoutePoint(
        string RouteId,
        NearbyJeepneyResponse Response);

    private sealed record FullRouteGeometry(
        List<(double Latitude, double Longitude)> Points,
        double[] CumulativeMeters);

    internal sealed record RouteAnchor(
        string RouteId,
        int SegmentIndex,
        double SegmentFraction,
        double Latitude,
        double Longitude,
        double DistanceFromRouteStartMeters);

    internal sealed record RouteConnectionCandidate(
        string RouteId,
        string RouteName,
        AccessCandidate BoardAccess,
        AccessCandidate AlightAccess,
        int BoardIndex,
        int AlightIndex,
        double TotalGeneralizedCostPesos);

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

    internal sealed record JourneyLegCandidate(
        string RouteId,
        string RouteName,
        (double Latitude, double Longitude) Board,
        (double Latitude, double Longitude) Alight,
        int? BoardIndex = null,
        int? AlightIndex = null,
        RouteAnchor? BoardFullRouteAnchor = null,
        RouteAnchor? AlightFullRouteAnchor = null);

    internal sealed record WalkSegmentCandidate(
        (double Latitude, double Longitude) From,
        (double Latitude, double Longitude) To,
        double StraightLineMeters);

    /// <summary>
    /// A terminal edge from a viable journey state. Implementations differ in
    /// how Valhalla confirms the final traversal, but all compete as complete
    /// plans before final Pareto/objective selection.
    /// </summary>
    internal abstract record DestinationCompletionEdge;

    internal sealed record JourneyCandidate(
        List<JourneyLegCandidate> Legs,
        AccessCandidate OriginAccess,
        AccessCandidate DestinationAccess,
        List<WalkSegmentCandidate> TransferWalkSegments,
        double? ProvisionalJourneyCostPesos = null) : DestinationCompletionEdge
    {
        public double TotalGeneralizedCostPesos =>
            ProvisionalJourneyCostPesos ?? double.PositiveInfinity;

        public int TransferCount =>
            Legs.Count - 1;
    }

    private sealed record DirectAccessDestinationCompletionEdge(
        AccessCandidate Access,
        double MaximumDistanceMeters) : DestinationCompletionEdge;

    internal sealed record AccessCandidate(
        AccessMode Mode,
        (double Latitude, double Longitude) Anchor,
        double WalkDistanceMeters,
        double WalkTimeSeconds,
        TrikePoint? TrikePoint,
        double? TrikeRideDistanceMeters,
        double? TrikeRideTimeSeconds,
        double? TrikeFarePesos,
        double ValueOfTimePesosPerMinute,
        double WalkingFatiguePesosPerKilometer,
        int? RouteSampleIndex = null,
        RouteAnchor? FullRouteAnchor = null,
        IReadOnlyList<AccessCandidate>? Alternatives = null)
    {
        public IReadOnlyList<AccessCandidate> AllAlternatives =>
            Alternatives ?? [this];

        public double TotalTimeSeconds =>
            WalkTimeSeconds +
            (TrikeRideTimeSeconds ?? 0);

        public double FarePesos =>
            TrikeFarePesos ?? 0;

        public double GeneralizedCostPesos =>
            FarePesos +
            TotalTimeSeconds / 60.0 * ValueOfTimePesosPerMinute +
            WalkDistanceMeters / 1_000 * WalkingFatiguePesosPerKilometer;
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
