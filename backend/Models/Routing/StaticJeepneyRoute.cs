namespace backend.Models.Routing;

public class StaticJeepneyRoute
{
    public string RouteId { get; set; } = string.Empty;

    public string RouteName { get; set; } = string.Empty;

    // [longitude, latitude]
    public List<double[]> Coordinates { get; set; } = [];
}