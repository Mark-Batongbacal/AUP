using backend.Models.Database;

namespace backend.Repositories;

public interface INavigationInstructionRepository
{
    Task ReplaceForSessionAsync(Guid sessionId, IReadOnlyList<NavigationInstruction> instructions, CancellationToken cancellationToken = default);
    Task<List<NavigationInstruction>> GetForOwnedSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
}
