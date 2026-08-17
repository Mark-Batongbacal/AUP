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
}
