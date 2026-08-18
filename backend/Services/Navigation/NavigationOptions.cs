namespace backend.Services.Navigation;

public sealed class NavigationOptions
{
    public const string SectionName = "Navigation";
    public double MaxGpsAccuracyMeters { get; init; } = 75;
    public int MaxLocationAgeSeconds { get; init; } = 120;
    public double MaxPlausibleSpeedMetersPerSecond { get; init; } = 45;
    public double MaxBackwardProgressMeters { get; init; } = 75;
    public double MaxForwardProgressMetersPerUpdate { get; init; } = 2_000;
    public int StateConfirmationSamples { get; init; } = 2;
    public double PrepareToAlightDistanceMeters { get; init; } = 400;
    public double ArrivalDistanceMeters { get; init; } = 35;
    public double MaximumLandmarkProjectionMeters { get; init; } = 100;
    public double MinimumLandmarkSeparationMeters { get; init; } = 250;
    public int MaximumLandmarksPerLeg { get; init; } = 5;
    public double LandmarkLookbackFromAlightMeters { get; init; } = 1_500;
    public double BoardReferenceMaximumDistanceMeters { get; init; } = 300;
    public double MinimumAlightReferenceLeadMeters { get; init; } = 15;
    public string TricycleRoadCosting { get; init; } = "auto";
    public double WalkingOffRouteMeters { get; init; } = 60;
    public double TransitOffRouteMeters { get; init; } = 150;
    public int OffRouteDurationSeconds { get; init; } = 20;
    public int MinimumOffRouteSamples { get; init; } = 3;
    public int RerouteCooldownSeconds { get; init; } = 120;
    public double MissedAlightDistanceMeters { get; init; } = 150;

    public bool IsValid() => MaxGpsAccuracyMeters > 0 && MaxLocationAgeSeconds > 0 &&
        MaxPlausibleSpeedMetersPerSecond > 0 && MaxBackwardProgressMeters >= 0 &&
        MaxForwardProgressMetersPerUpdate > 0 && StateConfirmationSamples >= 2 &&
        PrepareToAlightDistanceMeters > 0 && ArrivalDistanceMeters > 0 &&
        MaximumLandmarkProjectionMeters > 0 && MinimumLandmarkSeparationMeters >= 0 &&
        MaximumLandmarksPerLeg >= 2 && LandmarkLookbackFromAlightMeters > 0 &&
        BoardReferenceMaximumDistanceMeters > 0 && MinimumAlightReferenceLeadMeters >= 0 &&
        !string.IsNullOrWhiteSpace(TricycleRoadCosting) &&
        WalkingOffRouteMeters > 0 && TransitOffRouteMeters > 0 &&
        OffRouteDurationSeconds >= 0 && MinimumOffRouteSamples >= 2 &&
        RerouteCooldownSeconds >= 0 && MissedAlightDistanceMeters > 0;
}
