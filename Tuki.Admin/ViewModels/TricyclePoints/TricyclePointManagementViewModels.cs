using Tuki.Admin.Models.TricyclePoints;

namespace Tuki.Admin.ViewModels.TricyclePoints;

public sealed class TricyclePointListViewModel
{
    public IReadOnlyList<AdminTricyclePoint> Points { get; init; } = [];
    public bool IncludeArchived { get; init; } = true;
    public string Search { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalItems { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public string? ErrorMessage { get; init; }
}

public sealed class TricyclePointEditViewModel
{
    public long? TricyclePointId { get; init; }
    public AdminTricyclePointRequest Request { get; init; } = new();
    public IReadOnlyList<TricyclePointDuplicateWarning> DuplicateWarnings { get; init; } = [];
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }

    public bool IsEdit => TricyclePointId.HasValue;
    public string Title => IsEdit ? "Edit official tricycle point" : "Create official tricycle point";
}
