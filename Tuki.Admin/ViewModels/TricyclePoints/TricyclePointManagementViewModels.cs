using Tuki.Admin.Models.TricyclePoints;

namespace Tuki.Admin.ViewModels.TricyclePoints;

public sealed class TricyclePointListViewModel
{
    public IReadOnlyList<AdminTricyclePoint> Points { get; init; } = [];
    public bool IncludeArchived { get; init; } = true;
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
