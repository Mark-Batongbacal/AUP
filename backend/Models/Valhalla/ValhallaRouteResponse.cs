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
    // Valhalla sends its route geometry in this property.  It is retained for
    // decoding, but deliberately omitted from API responses in favour of Points.
    [JsonIgnore]
    public string Shape { get; private set; } = string.Empty;

    [JsonPropertyName("shape")]
    public string EncodedShape
    {
        set => Shape = value ?? string.Empty;
    }

    // Coordinates use [longitude, latitude], matching jeepney-routes.json.
    public List<double[]> Points { get; set; } = [];
}
