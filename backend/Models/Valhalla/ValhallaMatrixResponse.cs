using System.Text.Json.Serialization;

namespace backend.Models.Valhalla;

public class ValhallaMatrixResponse
{
    [JsonPropertyName("sources_to_targets")]
    // Valhalla returns a row for every source, even for a one-to-many query.
    public List<List<ValhallaMatrixResult>> SourcesToTargets { get; set; } = [];
}

public class ValhallaMatrixResult
{
    [JsonPropertyName("from_index")]
    public int FromIndex { get; set; }

    [JsonPropertyName("to_index")]
    public int ToIndex { get; set; }

    // The request asks for kilometres, so callers convert to metres as needed.
    public double? Distance { get; set; }
    public double? Time { get; set; }
}
