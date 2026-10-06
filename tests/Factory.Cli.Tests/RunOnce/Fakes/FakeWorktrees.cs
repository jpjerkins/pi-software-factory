using Factory.Application.Ports;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Cli.Tests.RunOnce.Fakes;

public sealed class FakeWorktrees : IWorktrees
{
    public int Prepared { get; private set; }

    public Task<Worktree> PrepareAsync(Issue issue, CancellationToken ct)
    {
        Prepared++;
        return Task.FromResult(new Worktree("/wt/" + issue.Number.Value, WorkBranch.For(issue).BranchName));
    }

    public Task RemoveAsync(Worktree worktree, CancellationToken ct) => Task.CompletedTask;
}
