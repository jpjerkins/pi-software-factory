using Factory.Domain.Issues;

namespace Factory.Domain.Runs;

public sealed record WorkerLaunch(RunId Run, IssueNumber Issue, Worktree Worktree);
