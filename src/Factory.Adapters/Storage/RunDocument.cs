using Factory.Domain.Runs;

namespace Factory.Adapters.Storage;

/// <summary>The shape of run.json.</summary>
internal sealed record RunDocument(
    string RunId,
    int Issue,
    WorktreeDocument Worktree,
    SessionDocument Session,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    WorkerOutcome Outcome,
    UsageDocument UsageBefore,
    UsageDocument UsageAfter)
{
    public static RunDocument From(Run run) => new(
        run.Id.Value,
        run.Issue.Value,
        new WorktreeDocument(run.Worktree.Path, run.Worktree.Branch),
        new SessionDocument(run.Session.PaneId, run.Session.AgentName, run.Session.SessionId),
        run.StartedAt,
        run.EndedAt,
        run.Outcome,
        new UsageDocument(run.UsageBefore.RawText, run.UsageBefore.ReadAt),
        new UsageDocument(run.UsageAfter.RawText, run.UsageAfter.ReadAt));
}

internal sealed record WorktreeDocument(string Path, string Branch);

internal sealed record SessionDocument(string PaneId, string AgentName, string SessionId);

internal sealed record UsageDocument(string RawText, DateTimeOffset ReadAt);
