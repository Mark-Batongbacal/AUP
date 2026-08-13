using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class FareRule
{
    public Guid FareRuleId { get; set; }

    public short TransportModeId { get; set; }

    public Guid? RouteId { get; set; }

    public string RuleName { get; set; } = null!;

    public decimal BaseFare { get; set; }

    public decimal? BaseDistanceKm { get; set; }

    public decimal? AdditionalFarePerKm { get; set; }

    public decimal? MinimumFare { get; set; }

    public decimal? MaximumFare { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual TransportRoute? Route { get; set; }

    public virtual TransportMode TransportMode { get; set; } = null!;
}
