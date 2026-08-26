using System.ComponentModel.DataAnnotations;
using Tuki.Admin.Models.TricycleSubmissions;

namespace Tuki.Admin.ViewModels.TricycleSubmissions;

public sealed class TricycleSubmissionQueueViewModel
{
    public IReadOnlyList<AdminTricycleSubmission> Items { get; init; } = [];
    public string Status { get; init; } = "Pending";
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }

    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed class TricycleSubmissionReviewViewModel
{
    public AdminTricycleSubmission Submission { get; init; } = null!;

    [Required]
    public decimal? Latitude { get; set; }

    [Required]
    public decimal? Longitude { get; set; }

    [StringLength(200)]
    public string? PointName { get; set; }

    [StringLength(200)]
    public string? OperatorName { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(300)]
    public string? Landmark { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(1000)]
    public string? AdminNotes { get; set; }

    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }

    public string GoogleMapsUrl =>
        $"https://www.google.com/maps/search/?api=1&query={Latitude?.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Longitude?.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static TricycleSubmissionReviewViewModel From(AdminTricycleSubmission submission) => new()
    {
        Submission = submission,
        Latitude = submission.Latitude,
        Longitude = submission.Longitude,
        PointName = submission.AdminPointName ?? submission.SuggestedTodaName,
        OperatorName = submission.AdminOperatorName,
        Address = submission.AdminAddress,
        Landmark = submission.AdminLandmark ?? submission.SuggestedLandmark,
        Description = submission.AdminDescription,
        AdminNotes = submission.AdminNotes
    };
}

public sealed class TricycleSubmissionDecisionViewModel
{
    [Required, StringLength(1000, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
