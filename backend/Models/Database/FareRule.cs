using System;
using System.Collections.Generic;

namespace backend.Models.Database;

public partial class FareRule
{
    public long FareRuleId { get; set; }

    public int TransportModeId { get; set; }

    public long RouteId { get; set; }

    public string PassengerType { get; set; } = null!;

    public string FareType { get; set; } = null!;

    public string RuleName { get; set; } = null!;

    public decimal BaseFare { get; set; }

    public decimal? BaseDistanceKm { get; set; }

    public int? IncludedDistanceMeters { get; set; }

    public decimal? AdditionalFarePerKm { get; set; }

    public decimal? MinimumFare { get; set; }

    public decimal? MaximumFare { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual TransportRoute Route { get; set; } = null!;

    public virtual TransportMode TransportMode { get; set; } = null!;
}
