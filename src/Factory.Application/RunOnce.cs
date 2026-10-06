using Factory.Application.Ports;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Application;

/// <summary>Takes one eligible issue up to the point where its worker session ends.</summary>
public sealed class RunOnce(
    IIssues issues,
    IWorktrees worktrees,
    IFolderTrust folderTrust,
    IWorkerSlots slots,
    IUsageProbe usage,
    IRunStore runs,
    IClock clock,
    RunOnceOptions options)
{
    private readonly NextIssue _nextIssue = new(issues);

    public async Task<RunOnceResult> ExecuteAsync(CancellationToken ct)
    {
        var issue = await _nextIssue.FindAsync(ct);
        if (issue is null)
        {
            return RunOnceResult.NoWork;
        }

        var usageBefore = await usage.ReadAsync(ct);
        var startedAt = clock.Now;
        var runId = RunId.From(startedAt, issue.Number);
        await runs.CreateAsync(runId, ct);

        await issues.ClaimAsync(issue.Number, runId, ct);
        var worktree = await worktrees.PrepareAsync(issue, ct);
        await folderTrust.EnsureTrustedAsync(worktree.Path, ct);
        var session = await slots.StartAsync(new WorkerLaunch(runId, issue.Number, worktree), ct);

        var outcome = await WaitForOutcomeAsync(runId, session, ct);

        var usageAfter = await usage.ReadAsync(ct);
        var run = new Run(runId, issue.Number, worktree, session, startedAt, clock.Now, outcome, usageBefore, usageAfter);
        await runs.SaveAsync(run, ct);
        return new RunOnceResult(run);
    }

    private async Task<WorkerOutcome> WaitForOutcomeAsync(RunId runId, WorkerSession session, CancellationToken ct)
    {
        while (true)
        {
            await clock.WaitAsync(options.PollInterval, ct);
            var result = await runs.ReadResultAsync(runId, ct);
            var signals = await runs.ReadSignalsAsync(runId, ct);
            var isRunning = await slots.IsRunningAsync(session, ct);
            var outcome = OutcomeDetection.Detect(result, signals, isRunning, clock.Now, options.StuckTimeout);
            if (outcome is { } detected)
            {
                return detected;
            }
        }
    }
}
