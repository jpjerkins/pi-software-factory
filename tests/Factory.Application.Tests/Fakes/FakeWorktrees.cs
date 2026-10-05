using Factory.Application.Ports;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Application.Tests.Fakes;

internal sealed class FakeWorktrees(CallLog log) : IWorktrees
{
    public List<IssueNumber> Prepared { get; } = [];
    public List<Worktree> Removed { get; } = [];

    public Task<Worktree> PrepareAsync(IssueNumber issue, CancellationToken ct)
    {
        log.Add("worktree");
        Prepared.Add(issue);
        return Task.FromResult(new Worktree($"/clone/wt-{issue.Value}", $"lane/{issue.Value}"));
    }

    public Task RemoveAsync(Worktree worktree, CancellationToken ct)
    {
        Removed.Add(worktree);
        return Task.CompletedTask;
    }
}
