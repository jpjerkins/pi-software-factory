using Factory.Application.Ports;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Application.Tests.Fakes;

internal sealed class FakeWorktrees(CallLog log) : IWorktrees
{
    public List<Issue> Prepared { get; } = [];
    public List<Worktree> Removed { get; } = [];

    public Task<Worktree> PrepareAsync(Issue issue, CancellationToken ct)
    {
        log.Add("worktree");
        Prepared.Add(issue);
        return Task.FromResult(new Worktree($"/repo/wt-{issue.Number.Value}", WorkBranch.For(issue).BranchName));
    }

    public Task RemoveAsync(Worktree worktree, CancellationToken ct)
    {
        Removed.Add(worktree);
        return Task.CompletedTask;
    }
}
