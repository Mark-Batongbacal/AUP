using backend.Models.Routing;
using backend.Models.Valhalla;

namespace backend.Services.Routing;

public partial class RoutingService
{
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

    private sealed record DirectTripCandidate(
        AccessCandidate Access,
        double MaximumDistanceMeters);

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
