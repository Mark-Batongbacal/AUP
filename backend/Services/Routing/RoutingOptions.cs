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
    /// so no fastest/cheapest advantage can excuse it (see
    /// PruneConfirmedFeederShadowing / PruneConfirmedTransferBoardingShadowing).
    /// </summary>
    public double FeederShadowingAccessDistanceRatio { get; init; } = 0.60;

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
            BoardingDiversityBucketMeters <= 0 ||
            JourneyLegContinuityToleranceMeters <= 0 ||
            string.IsNullOrWhiteSpace(TrikeCostingModel))
        {
            error = "Routing distances, fares, and time values must be non-negative; speeds, sampling, diversity, and continuity values must be positive.";
            return false;
        }

        if (FeederShadowingAccessDistanceRatio is <= 0 or > 1)
        {
            error = "Feeder shadowing access-distance ratio must be greater than zero and at most one.";
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