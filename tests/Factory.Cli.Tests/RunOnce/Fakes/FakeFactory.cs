using Factory.Application;
using Factory.Domain.Issues;

namespace Factory.Cli.Tests.RunOnce.Fakes;

/// <summary>The Application use cases wired to fakes, with the fakes kept for inspection.</summary>
public sealed class FakeFactory
{
    public FakeFactory(params Issue[] open) : this(new FakeIssues(open))
    {
    }

    public FakeFactory(FakeIssues issues)
    {
        Issues = issues;
        RunOnce = new Factory.Application.RunOnce(
            issues, Worktrees, FolderTrust, Slots, new FakeUsageProbe(), Runs, new FakeClock(),
            new RunOnceOptions("/repo", TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(30)));
        NextIssue = new NextIssue(issues);
    }

    public FakeIssues Issues { get; }
    public FakeWorktrees Worktrees { get; } = new();
    public FakeFolderTrust FolderTrust { get; } = new();
    public FakeWorkerSlots Slots { get; } = new();
    public FakeRunStore Runs { get; } = new();
    public Factory.Application.RunOnce RunOnce { get; }
    public NextIssue NextIssue { get; }

    public static Issue Eligible(int number, string title) => new(
        new IssueNumber(number), title, true, FakeClock.Start,
        ["build", "agent:claude", "lane:feat"], [], [], []);

    public bool TouchedNothing =>
        Issues.Claimed.Count == 0 && Worktrees.Prepared == 0 && FolderTrust.Calls == 0 && Slots.Started == 0 && Runs.Created == 0;
}
