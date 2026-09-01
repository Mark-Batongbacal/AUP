using System.ComponentModel.DataAnnotations;

namespace backend.Models.TricyclePointManagement;

public sealed class AdminTricyclePointMutationRequest
{
    [Required, StringLength(100)]
    public string PointCode { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string PointName { get; init; } = string.Empty;

    [Required, Range(-90d, 90d)]
    public double? Latitude { get; init; }

    [Required, Range(-180d, 180d)]
    public double? Longitude { get; init; }

    [Range(1, 10000)]
    public int RadiusMeters { get; init; } = 500;

    public long? StopId { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    [StringLength(200)]
    public string? OperatorName { get; init; }

    [Range(typeof(decimal), "0", "100000")]
    public decimal? BaseFare { get; init; }

    [Range(typeof(decimal), "0", "100000")]
    public decimal? FarePerKilometer { get; init; }

    [Range(0, 86400)]
    public int? AverageWaitingTimeSeconds { get; init; }

    public TimeOnly? ServiceStartTime { get; init; }
    public TimeOnly? ServiceEndTime { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record AdminTricyclePointResponse(
    long TricyclePointId,
    long? StopId,
    string PointCode,
    string PointName,
    string? Description,
    string? Address,
    string? OperatorName,
    double Latitude,
    double Longitude,
    int RadiusMeters,
    decimal? BaseFare,
    decimal? FarePerKilometer,
    int? AverageWaitingTimeSeconds,
    TimeOnly? ServiceStartTime,
    TimeOnly? ServiceEndTime,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record TricyclePointDuplicateWarning(
    long TricyclePointId,
    string PointCode,
    string PointName,
    double DistanceMeters,
    bool IsActive);

public sealed record AdminTricyclePointMutationResponse(
    AdminTricyclePointResponse Point,
    IReadOnlyList<TricyclePointDuplicateWarning> DuplicateWarnings);

public sealed record AdminTricyclePointMutationResult(
    bool Succeeded,
    bool NotFound,
    bool Conflict,
    IReadOnlyList<string> Errors,
    AdminTricyclePointMutationResponse? Response)
{
    public static AdminTricyclePointMutationResult Success(AdminTricyclePointMutationResponse response) =>
        new(true, false, false, [], response);

    public static AdminTricyclePointMutationResult Missing() =>
        new(false, true, false, [], null);

    public static AdminTricyclePointMutationResult Invalid(IReadOnlyList<string> errors) =>
        new(false, false, false, errors, null);

    public static AdminTricyclePointMutationResult StateConflict(IReadOnlyList<string> errors) =>
        new(false, false, true, errors, null);
}
