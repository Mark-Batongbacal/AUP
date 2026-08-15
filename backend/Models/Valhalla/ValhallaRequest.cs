namespace backend.Models.Valhalla;

public class ValhallaRouteRequest
{
    public List<ValhallaLocation> Locations { get; set; } = [];
    public string Costing { get; set; } = "pedestrian";
}

public class ValhallaLocation
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class ValhallaMatrixRequest
{
    public List<ValhallaLocation> Sources { get; set; } = [];
    public List<ValhallaLocation> Targets { get; set; } = [];
    public string Costing { get; set; } = "pedestrian";
    public string Units { get; set; } = "kilometers";
    public bool Verbose { get; set; } = true;
}
