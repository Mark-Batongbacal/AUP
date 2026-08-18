using backend.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

public sealed class NavigationInstructionRepository(TukiDbContext context) : INavigationInstructionRepository
{
    public async Task ReplaceForSessionAsync(Guid sessionId, IReadOnlyList<NavigationInstruction> instructions, CancellationToken cancellationToken = default)
    {
        var existing = await context.NavigationInstructions
            .Where(item => item.TripSessionId == sessionId).ToListAsync(cancellationToken);
        context.NavigationInstructions.RemoveRange(existing);
        await context.NavigationInstructions.AddRangeAsync(instructions, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<NavigationInstruction>> GetForOwnedSessionAsync(
        Guid sessionId, Guid userId, CancellationToken cancellationToken = default) =>
        context.NavigationInstructions.AsNoTracking()
            .Where(item => item.TripSessionId == sessionId && item.TripSession.UserId == userId)
            .OrderBy(item => item.Sequence).ToListAsync(cancellationToken);
}
