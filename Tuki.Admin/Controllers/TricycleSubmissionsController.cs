using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tuki.Admin.Models.TricycleSubmissions;
using Tuki.Admin.Repositories.TricycleSubmissions;
using Tuki.Admin.ViewModels.TricycleSubmissions;

namespace Tuki.Admin.Controllers;

[Authorize(Roles = "Admin")]
public sealed class TricycleSubmissionsController(
    IAdminTricycleSubmissionRepository repository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string status = "Pending",
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        const int pageSize = 20;

        try
        {
            var result = await repository.GetPageAsync(status, page, pageSize, cancellationToken);
            if (!result.Succeeded || result.Value is null)
            {
                return View(new TricycleSubmissionQueueViewModel
                {
                    Status = status,
                    Page = page,
                    PageSize = pageSize,
                    ErrorMessage = result.ErrorMessage ?? "Unable to load submissions."
                });
            }

            return View(new TricycleSubmissionQueueViewModel
            {
                Items = result.Value.Items,
                Status = status,
                Page = result.Value.Page,
                PageSize = result.Value.PageSize,
                TotalCount = result.Value.TotalCount
            });
        }
        catch (InvalidOperationException)
        {
            return RedirectToAction(
                "Login",
                "Account",
                new { returnUrl = Url.Action(nameof(Index), "TricycleSubmissions") });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Review(long id, CancellationToken cancellationToken = default)
    {
        var result = await repository.GetByIdAsync(id, cancellationToken);
        if (result.StatusCode == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }

        if (!result.Succeeded || result.Value is null)
        {
            TempData["AdminError"] = result.ErrorMessage ?? "Unable to load the submission.";
            return RedirectToAction(nameof(Index));
        }

        var model = TricycleSubmissionReviewViewModel.From(result.Value)
            .WithMessages(
                TempData["AdminError"] as string,
                TempData["AdminSuccess"] as string);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReview(
        long id,
        TricycleSubmissionReviewViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid || model.Latitude is null || model.Longitude is null)
        {
            var current = await repository.GetByIdAsync(id, cancellationToken);
            if (!current.Succeeded || current.Value is null)
            {
                return RedirectToAction(nameof(Index));
            }

            model = MergeSubmission(model, current.Value);
            return View("Review", model);
        }

        var result = await repository.UpdateReviewAsync(
            id,
            new AdminTricycleReviewRequest
            {
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                PointName = model.PointName,
                OperatorName = model.OperatorName,
                Address = model.Address,
                Landmark = model.Landmark,
                Description = model.Description,
                AdminNotes = model.AdminNotes
            },
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to save review changes.");
            var current = await repository.GetByIdAsync(id, cancellationToken);
            if (current.Value is not null)
            {
                model = MergeSubmission(model, current.Value);
            }
            return View("Review", model);
        }

        TempData["AdminSuccess"] = "Review details and coordinates saved.";
        return RedirectToAction(nameof(Review), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        long id,
        TricycleSubmissionDecisionViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminError"] = "Enter a rejection reason of at least 3 characters.";
            return RedirectToAction(nameof(Review), new { id });
        }

        var result = await repository.RejectAsync(id, model.Reason, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["AdminError"] = result.ErrorMessage ?? "Unable to reject the submission.";
            return RedirectToAction(nameof(Review), new { id });
        }

        TempData["AdminSuccess"] = "Submission rejected.";
        return RedirectToAction(nameof(Review), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NeedsChanges(
        long id,
        TricycleSubmissionDecisionViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminError"] = "Enter what needs to be corrected before returning this submission.";
            return RedirectToAction(nameof(Review), new { id });
        }

        var result = await repository.NeedsChangesAsync(id, model.Reason, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["AdminError"] = result.ErrorMessage ?? "Unable to mark the submission as needing changes.";
            return RedirectToAction(nameof(Review), new { id });
        }

        TempData["AdminSuccess"] = "Submission marked as needing changes.";
        return RedirectToAction(nameof(Review), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Proof(long id, CancellationToken cancellationToken = default)
    {
        var submission = await repository.GetByIdAsync(id, cancellationToken);
        if (!submission.Succeeded || submission.Value is null)
        {
            return NotFound();
        }

        var proof = await repository.GetProofAsync(submission.Value.ProofImageUrl, cancellationToken);
        if (!proof.Succeeded || proof.Value is null)
        {
            return NotFound();
        }

        return File(proof.Value.Bytes, proof.Value.ContentType);
    }

    private static TricycleSubmissionReviewViewModel MergeSubmission(
        TricycleSubmissionReviewViewModel model,
        AdminTricycleSubmission submission) => new()
    {
        Submission = submission,
        Latitude = model.Latitude,
        Longitude = model.Longitude,
        PointName = model.PointName,
        OperatorName = model.OperatorName,
        Address = model.Address,
        Landmark = model.Landmark,
        Description = model.Description,
        AdminNotes = model.AdminNotes
    };
}

internal static class TricycleSubmissionReviewViewModelExtensions
{
    public static TricycleSubmissionReviewViewModel WithMessages(
        this TricycleSubmissionReviewViewModel model,
        string? error,
        string? success) => new()
    {
        Submission = model.Submission,
        Latitude = model.Latitude,
        Longitude = model.Longitude,
        PointName = model.PointName,
        OperatorName = model.OperatorName,
        Address = model.Address,
        Landmark = model.Landmark,
        Description = model.Description,
        AdminNotes = model.AdminNotes,
        ErrorMessage = error,
        SuccessMessage = success
    };
}
