using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

public sealed class TricyclePointSubmissionRepository(TukiDbContext context)
    : ITricyclePointSubmissionRepository
{
    private readonly TukiDbContext _context = context;

    public async Task<TricyclePointSubmission> AddAsync(
        TricyclePointSubmission submission,
        CancellationToken cancellationToken = default)
    {
        await _context.TricyclePointSubmissions.AddAsync(submission, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return submission;
    }

    public Task<TricyclePointSubmission?> GetByIdAsync(
        long submissionId,
        CancellationToken cancellationToken = default) =>
        _context.TricyclePointSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                submission => submission.TricyclePointSubmissionId == submissionId,
                cancellationToken);

    public Task<List<TricyclePointSubmission>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _context.TricyclePointSubmissions
            .AsNoTracking()
            .Where(submission => submission.SubmittedByUserId == userId)
            .OrderByDescending(submission => submission.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<(List<TricyclePointSubmission> Items, int TotalCount)> GetForAdminAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TricyclePointSubmission> query = _context.TricyclePointSubmissions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(submission => submission.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(submission => submission.CreatedAt)
            .ThenByDescending(submission => submission.TricyclePointSubmissionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<TricyclePointSubmission?> GetTrackedByIdAsync(
        long submissionId,
        CancellationToken cancellationToken = default) =>
        _context.TricyclePointSubmissions
            .FirstOrDefaultAsync(
                submission => submission.TricyclePointSubmissionId == submissionId,
                cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
