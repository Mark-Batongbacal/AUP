namespace backend.Models.Database;

public enum NavigationInstructionType
{
    Continue,
    TurnLeft,
    TurnRight,
    Roundabout,
    BoardJeepney,
    PrepareToAlight,
    AlightJeepney,
    BoardTricycle,
    AlightTricycle,
    Transfer,
    LandmarkNotice,
    Arrived
}

public enum NavigationInstructionAudience
{
    Passenger,
    Driver
}

public sealed class NavigationInstruction
{
    public Guid NavigationInstructionId { get; set; }
    public Guid TripSessionId { get; set; }
    public int Sequence { get; set; }
    public NavigationInstructionType Type { get; set; }
    public NavigationInstructionAudience Audience { get; set; }
    public int LegIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? StreetName { get; set; }
    public int? SourceManeuverType { get; set; }
    public int? BeginShapeIndex { get; set; }
    public int? EndShapeIndex { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? DistanceFromLegStartMeters { get; set; }
    public double? DistanceFromRouteStartMeters { get; set; }
    public double TriggerDistanceMeters { get; set; }
    public bool RequiresConfirmation { get; set; }
    public TripSession TripSession { get; set; } = null!;
}
