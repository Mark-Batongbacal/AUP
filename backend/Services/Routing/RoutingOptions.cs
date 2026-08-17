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
    public int MaxNearbyTrikeCandidates { get; init; } = 3;
    public double MaxWalkToTrikePointMeters { get; init; } = 1_000;
    public double MaxWalkOnlyTripDistanceMeters { get; init; } = 2_000;
    public double MaxWalkTrikeTripDistanceMeters { get; init; } = 5_000;
    public double MaxWalkAccessDistanceMeters { get; init; } = 1_500;
    public double MaxTotalWalkingMetersPerJourney { get; init; } = 2_500;
    public double MaxStaticRouteSegmentJumpMeters { get; init; } = 10_000;
    public double ServiceAreaMinLatitude { get; init; } = 14.8;
    public double ServiceAreaMaxLatitude { get; init; } = 15.35;
    public double ServiceAreaMinLongitude { get; init; } = 120.35;
    public double ServiceAreaMaxLongitude { get; init; } = 120.9;
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

    public bool IsValid(out string error)
    {
        if (MaxNearbyRoutes <= 0 || MaxTripOptions <= 0 || MaxRouteSamples < 2 ||
            MatrixMaxTargets <= 0 || MaxInterchangesPerRoutePair <= 0 ||
            MaxNearbyTrikeCandidates < 0 || MaxCandidatesToConfirm <= 0)
        {
            error = "Routing count limits must be positive (except MaxNearbyTrikeCandidates, which may be zero).";
            return false;
        }

        if (DefaultSampleIntervalMeters <= 0 || WalkingSpeedMetersPerSecond <= 0 ||
            TrikeSpeedMetersPerSecond <= 0 || JeepneySpeedMetersPerSecond <= 0 ||
            MaxTransferWalkMeters < 0 || MaxWalkToTrikePointMeters < 0 ||
            MaxWalkOnlyTripDistanceMeters < 0 || MaxWalkTrikeTripDistanceMeters < 0 ||
            MaxWalkAccessDistanceMeters < 0 || TrikeBaseFarePesos < 0 ||
            TrikeBaseDistanceMeters < 0 || TrikePerAdditionalKmPesos < 0 ||
            ValueOfTimePesosPerMinute < 0 || WalkingFatiguePesosPerKilometer < 0 ||
            JeepneyBoardingWaitTimeSeconds < 0 ||
            JeepneyBaseFarePesos < 0 || string.IsNullOrWhiteSpace(TrikeCostingModel))
        {
            error = "Routing distances, fares, and time values must be non-negative; speeds and sample interval must be positive.";
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
