using System.Data;
using backend.Models.Database;
using backend.Models.TricyclePointSubmissions;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

public sealed class TricyclePointSubmissionPublishingRepository(TukiDbContext context)
    : ITricyclePointSubmissionPublishingRepository
{
    private readonly TukiDbContext _context = context;

    public async Task<TricyclePointSubmissionPublishResult> PublishAsync(
        long submissionId,
        Guid adminUserId,
        TricyclePointPublicationDraft draft,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var submission = await _context.TricyclePointSubmissions
                .FirstOrDefaultAsync(
                    item => item.TricyclePointSubmissionId == submissionId,
                    cancellationToken);

            if (submission is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TricyclePointSubmissionPublishResult.Missing();
            }

            if (submission.PublishedTricyclePointId is not null ||
                submission.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync(cancellationToken);
                return TricyclePointSubmissionPublishResult.StateConflict(
                    "This submission has already been published.");
            }

            if (!submission.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                !submission.Status.Equals("NeedsChanges", StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync(cancellationToken);
                return TricyclePointSubmissionPublishResult.StateConflict(
                    $"Submission status '{submission.Status}' cannot be approved.");
            }

            var existingCode = await _context.TricyclePoints
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    point => point.PointCode == draft.PointCode,
                    cancellationToken);

            if (existingCode is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TricyclePointSubmissionPublishResult.StateConflict(
                    $"Official point code '{draft.PointCode}' is already in use.");
            }

            var now = DateTimeOffset.UtcNow;
            var point = new TricyclePoint
            {
                StopId = null,
                PointCode = draft.PointCode,
                PointName = draft.PointName,
                Description = draft.Description,
                Address = draft.Address,
                OperatorName = draft.OperatorName,
                CenterLatitude = draft.Latitude,
                CenterLongitude = draft.Longitude,
                RadiusMeters = draft.RadiusMeters,
                BaseFare = null,
                FarePerKilometer = null,
                AverageWaitingTimeSeconds = null,
                ServiceStartTime = null,
                ServiceEndTime = null,
                IsActive = true,
                CreatedAt = now.UtcDateTime,
                UpdatedAt = now.UtcDateTime
            };

            await _context.TricyclePoints.AddAsync(point, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            submission.Status = "Approved";
            submission.PublishedTricyclePointId = point.TricyclePointId;
            submission.ReviewedByUserId = adminUserId;
            submission.ReviewedAt = now;
            submission.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return TricyclePointSubmissionPublishResult.Success(
                new TricyclePointSubmissionPublishResponse(
                    submission.TricyclePointSubmissionId,
                    point.TricyclePointId,
                    point.PointCode,
                    point.PointName,
                    submission.Status,
                    adminUserId,
                    now));
        });
    }
}
