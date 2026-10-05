using Factory.Domain.Issues;

namespace Factory.Domain.Runs;

/// <summary>The record of one run (the content of run.json).</summary>
public sealed record Run(
    RunId Id,
    IssueNumber Issue,
    Worktree Worktree,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    WorkerOutcome Outcome,
    UsageReading UsageBefore,
    UsageReading UsageAfter);
