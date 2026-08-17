namespace backend.Models.Database;

public sealed class RouteWaypoint
{
    public long RouteWaypointId { get; set; }
    public long RouteId { get; set; }
    public int WaypointOrder { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CreatedAt { get; set; }
    public TransportRoute Route { get; set; } = null!;
}
