namespace backend.Services.Routing;

/// <summary>
/// Tunable routing behavior. Distances are metres and times are seconds unless
/// otherwise stated. Tricycle routing uses Valhalla's configured road profile
/// only as an MVP road-network proxy; it does not model tricycle legality.
/// </summary>
public sealed class RoutingOptions
{
    public const string SectionName = "Routing";

    public int MaxNearbyRoutes { get; init; } = 20;
    public int MaxTripOptions { get; init; } = 10;
    public double DefaultSampleIntervalMeters { get; init; } = 150;
    public int MaxRouteSamples { get; init; } = 40;
    // Maximum targets in a one-source matrix call. The source is additional.
    public int MatrixMaxTargets { get; init; } = 99;
    public int MaxInterchangesPerRoutePair { get; init; } = 4;
    public double MaxTransferWalkMeters { get; init; } = 400;
    /// <summary>
    /// Provisional (pre-Valhalla) boarding variants kept per route before
    /// confirmation. One slot is always reserved for the earliest full-route
    /// board progress so a sensible nearby boarding opportunity cannot be
    /// crowded out by cost/time/fare heuristics computed on unconfirmed,
    /// straight-line access estimates.
    /// </summary>
    public int MaxBoardingVariantsPerRoute { get; init; } = 5;
    /// <summary>
    /// Minimum forward route progress skipped by a same-route transfer. The
    /// 1 km default is deliberately well above the normal 150 m sample spacing
    /// so adjacent route samples cannot be mistaken for a new boarding.
    /// </summary>
    public double MinimumSelfTransferProgressMeters { get; init; } = 1_000;
    /// <summary>
    /// Minimum ratio of skipped route distance to straight-line transfer
    /// distance. This removes same-route transfers that offer no real shortcut;
    /// confirmed pedestrian distance is still authoritative during validation.
    /// </summary>
    public double MinimumSelfTransferRouteToWalkRatio { get; init; } = 3;
    public int MaxNearbyTrikeCandidates { get; init; } = 3;
    public double MaxWalkToTrikePointMeters { get; init; } = 1_000;
    public double MaxWalkOnlyTripDistanceMeters { get; init; } = 2_000;
    public double MaxWalkTrikeTripDistanceMeters { get; init; } = 5_000;
    public double MaxWalkAccessDistanceMeters { get; init; } = 1_500;
    public double MaxTotalWalkingMetersPerJourney { get; init; } = 2_500;

    /// <summary>
    /// Minimum downstream jeepney-route progress before a farther boarding
    /// point can be classified as feeder shadowing. Smaller differences are
    /// treated as normal stop/intersection choice noise.
    /// </summary>
    public double FeederShadowingMinProgressMeters { get; init; } = 300;

    /// <summary>
    /// Fraction of downstream jeepney progress that must also appear as extra
    /// confirmed feeder distance before the feeder is considered to be chasing
    /// the same jeepney corridor. Once this threshold is crossed the farther
    /// board is a feeder replacing transit, not a network-access optimization,
    /// so no fastest/cheapest advantage can excuse it. The same ratio governs
    /// all three boundaries: origin boarding, transfer boarding, and
    /// destination alighting (see RoutingService.FeederShadowing).
    /// </summary>
    public double FeederShadowingAccessDistanceRatio { get; init; } = 0.60;

    /// <summary>
    /// How far apart two boarding or alighting positions may sit along a
    /// route while still counting as the same position when deciding whether
    /// two journeys are the same journey.
    ///
    /// This is a TOLERANCE, not a bucket. Bucketing progress into fixed
    /// windows split journeys a passenger would call identical whenever two
    /// positions a few metres apart happened to straddle a window edge, and
    /// those journeys were then never compared for feeder shadowing at all.
    /// </summary>
    public double FeederShadowEquivalentProgressToleranceMeters { get; init; } = 300;

    /// <summary>
    /// How many times its own jeepney distance a journey's feeder legs may
    /// cover before the jeepney counts as a token gesture rather than
    /// transport -- a 30 m jeepney hop wrapped in a 2 km tricycle.
    ///
    /// The default of 2 leaves plenty of room for ordinary feeder shapes (a
    /// 500 m jeepney with a 400 m tricycle, or a 6 km jeepney with a 2 km
    /// destination tricycle are both comfortably clear of it) and only fires
    /// on journeys where the feeder modes are plainly making the trip. It is
    /// applied only when a sensible alternative journey survives; see
    /// PruneTokenTransitJourneys.
    /// </summary>
    public double TokenTransitJeepneyMultiple { get; init; } = 2;

    /// <summary>
    /// Jeepney distance a journey must carry before the jeepney counts as the
    /// journey's PRIMARY corridor mode. Below this the jeepney leg is an
    /// incidental hop rather than the backbone of the trip, so the journey is
    /// not treated as a practical public-transport option and the default
    /// recommendation falls back to ordinary balanced scoring. This is
    /// deliberately not a ban on short jeepney legs: it only decides whether a
    /// jeepney journey is strong enough to be PREFERRED as the default.
    /// </summary>
    public double PrimaryJeepneyMinimumDistanceMeters { get; init; } = 2_000;

    /// <summary>
    /// Share of a journey's total distance the jeepney legs must cover before
    /// the jeepney counts as the journey's primary corridor mode. This is what
    /// keeps feeder modes in a feeder role: a journey whose walking/tricycle
    /// legs cover most of the ground is not a jeepney journey, whatever its
    /// generalized cost happens to say.
    /// </summary>
    public double PrimaryJeepneyMinimumJourneyShare { get; init; } = 0.5;

    /// <summary>
    /// Full-route progress bucket used when reserving confirmation capacity for
    /// spatially distinct boarding regions. This prevents one dense cluster of
    /// nearly-identical anchors from crowding out other useful route variants.
    /// </summary>
    public double BoardingDiversityBucketMeters { get; init; } = 500;

    /// <summary>
    /// Maximum tolerated gap between consecutive physical journey legs and
    /// between a leg endpoint and its enriched geometry endpoint.
    /// </summary>
    public double JourneyLegContinuityToleranceMeters { get; init; } = 25;

    public double MaxStaticRouteSegmentJumpMeters { get; init; } = 10_000;
    public double ServiceAreaMinLatitude { get; init; } = 14.8;
    public double ServiceAreaMaxLatitude { get; init; } = 15.35;
    public double ServiceAreaMinLongitude { get; init; } = 120.35;
    public double ServiceAreaMaxLongitude { get; init; } = 120.9;
    public double MaxSupportedTripStraightLineMeters { get; init; } = 75_000;
    public double TrikeBaseFarePesos { get; init; } = 35;
    public double TrikeBaseDistanceMeters { get; init; } = 1_000;
    public double TrikePerAdditionalKmPesos { get; init; } = 15;
    public double ValueOfTimePesosPerMinute { get; init; } = 3;
    /// <summary>Additional generalized-cost penalty for each kilometre walked.</summary>
    public double WalkingFatiguePesosPerKilometer { get; init; } = 4;
    public double WalkingSpeedMetersPerSecond { get; init; } = 1.2;
    public double TrikeSpeedMetersPerSecond { get; init; } = 5.6;
    public double JeepneySpeedMetersPerSecond { get; init; } = 6.5;
    public double JeepneyBoardingWaitTimeSeconds { get; init; } = 300;
    public double JeepneyBaseFarePesos { get; init; } = 13;
    public string TrikeCostingModel { get; init; } = "auto";
    public int MaxCandidatesToConfirm { get; init; } = 100;
    public int MaxTransfers { get; init; } = 2;

    public bool IsValid(out string error)
    {
        if (MaxNearbyRoutes <= 0 || MaxTripOptions <= 0 || MaxRouteSamples < 2 ||
            MatrixMaxTargets <= 0 || MaxInterchangesPerRoutePair <= 0 ||
            MaxNearbyTrikeCandidates < 0 || MaxCandidatesToConfirm <= 0 ||
            MaxBoardingVariantsPerRoute <= 0 ||
            MaxTransfers is < 0 or > 5)
        {
            error = "Routing count limits must be positive (except MaxNearbyTrikeCandidates, which may be zero).";
            return false;
        }

        if (DefaultSampleIntervalMeters <= 0 || WalkingSpeedMetersPerSecond <= 0 ||
            TrikeSpeedMetersPerSecond <= 0 || JeepneySpeedMetersPerSecond <= 0 ||
            MaxTransferWalkMeters < 0 || MaxWalkToTrikePointMeters < 0 ||
            MinimumSelfTransferProgressMeters <= 0 ||
            MinimumSelfTransferRouteToWalkRatio <= 1 ||
            MaxWalkOnlyTripDistanceMeters < 0 || MaxWalkTrikeTripDistanceMeters < 0 ||
            MaxWalkAccessDistanceMeters < 0 || TrikeBaseFarePesos < 0 ||
            MaxSupportedTripStraightLineMeters <= 0 ||
            TrikeBaseDistanceMeters < 0 || TrikePerAdditionalKmPesos < 0 ||
            ValueOfTimePesosPerMinute < 0 || WalkingFatiguePesosPerKilometer < 0 ||
            JeepneyBoardingWaitTimeSeconds < 0 || JeepneyBaseFarePesos < 0 ||
            FeederShadowingMinProgressMeters < 0 ||
            FeederShadowEquivalentProgressToleranceMeters < 0 ||
            TokenTransitJeepneyMultiple <= 0 ||
            BoardingDiversityBucketMeters <= 0 ||
            JourneyLegContinuityToleranceMeters <= 0 ||
            string.IsNullOrWhiteSpace(TrikeCostingModel))
        {
            error = "Routing distances, fares, and time values must be non-negative; speeds, sampling, diversity, token-transit, and continuity values must be positive.";
            return false;
        }

        if (FeederShadowingAccessDistanceRatio is <= 0 or > 1)
        {
            error = "Feeder shadowing access-distance ratio must be greater than zero and at most one.";
            return false;
        }

        if (PrimaryJeepneyMinimumJourneyShare is <= 0 or > 1 ||
            PrimaryJeepneyMinimumDistanceMeters < 0)
        {
            error = "Primary jeepney share must be greater than zero and at most one, and its minimum distance must be non-negative.";
            return false;
        }

        if (MaxTotalWalkingMetersPerJourney <= 0 ||
            MaxStaticRouteSegmentJumpMeters <= 0 ||
            ServiceAreaMinLatitude >= ServiceAreaMaxLatitude ||
            ServiceAreaMinLongitude >= ServiceAreaMaxLongitude)
        {
            error = "Routing walking, static-geometry, and service-area bounds are invalid.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}