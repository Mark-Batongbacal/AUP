using System.ComponentModel.DataAnnotations;

namespace Tuki.Admin.Models.TricyclePoints;

public sealed record AdminTricyclePoint(
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
    AdminTricyclePoint Point,
    IReadOnlyList<TricyclePointDuplicateWarning> DuplicateWarnings);

public sealed class AdminTricyclePointRequest
{
    [Required, StringLength(100)]
    public string PointCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string PointName { get; set; } = string.Empty;

    [Required, Range(-90d, 90d)]
    public double? Latitude { get; set; }

    [Required, Range(-180d, 180d)]
    public double? Longitude { get; set; }

    [Range(1, 10000)]
    public int RadiusMeters { get; set; } = 500;

    public long? StopId { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(200)]
    public string? OperatorName { get; set; }

    [Range(typeof(decimal), "0", "100000")]
    public decimal? BaseFare { get; set; }

    [Range(typeof(decimal), "0", "100000")]
    public decimal? FarePerKilometer { get; set; }

    [Range(0, 86400)]
    public int? AverageWaitingTimeSeconds { get; set; }

    public TimeOnly? ServiceStartTime { get; set; }
    public TimeOnly? ServiceEndTime { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record AdminBackendError(IReadOnlyList<string> Errors);

public sealed record AdminPointRepositoryResult<T>(
    bool Succeeded,
    int StatusCode,
    T? Value,
    string? ErrorMessage)
{
    public static AdminPointRepositoryResult<T> Success(T value, int statusCode = 200) =>
        new(true, statusCode, value, null);

    public static AdminPointRepositoryResult<T> Failure(int statusCode, string message) =>
        new(false, statusCode, default, message);
}
