using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Application.Ports;

public interface IWorktrees
{
    Task<Worktree> PrepareAsync(Issue issue, CancellationToken ct);

    Task RemoveAsync(Worktree worktree, CancellationToken ct);
}
