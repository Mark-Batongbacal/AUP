using System.Text.Json.Serialization;

namespace backend.Models.Valhalla;

public class ValhallaRouteResponse
{
    public ValhallaTrip? Trip { get; set; }
}

public class ValhallaTrip
{
    public ValhallaSummary? Summary { get; set; }

    public List<ValhallaLeg> Legs { get; set; } = [];
}

public class ValhallaSummary
{
    public double Length { get; set; }

    public double Time { get; set; }
}

public class ValhallaLeg
{
    [JsonPropertyName("shape")]
    public string Shape { get; set; } = string.Empty;

    public List<double[]> Points { get; set; } = [];

    public List<ValhallaManeuver> Maneuvers { get; set; } = [];
}

public sealed class ValhallaManeuver
{
    public int Type { get; set; }
    public string Instruction { get; set; } = string.Empty;
    [JsonPropertyName("street_names")]
    public List<string> StreetNames { get; set; } = [];
    [JsonPropertyName("begin_shape_index")]
    public int BeginShapeIndex { get; set; }
    [JsonPropertyName("end_shape_index")]
    public int EndShapeIndex { get; set; }
    public double Length { get; set; }
    public double Time { get; set; }
}
