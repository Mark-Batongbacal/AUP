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
}
