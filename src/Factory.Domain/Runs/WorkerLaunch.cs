using Factory.Domain.Issues;

namespace Factory.Domain.Runs;

/// <summary>
/// What a worker is started with. <paramref name="IssueTitle"/> and <paramref name="IssueBody"/> are the ticket text:
/// workers cannot run <c>gh</c>, so the factory hands it over.
/// </summary>
public sealed record WorkerLaunch(
    RunId Run, IssueNumber Issue, Worktree Worktree, string IssueTitle = "", string IssueBody = "");
