using Factory.Application;
using Factory.Domain.Issues;

namespace Factory.Cli.RunOnce;

/// <summary>Runs <c>factory run-once</c>: take one issue to the end of its worker session, or just say which one would be taken.</summary>
public sealed class RunOnceCommand(
    RunOnceSettings settings,
    Factory.Application.RunOnce runOnce,
    NextIssue nextIssue,
    TextWriter stdout,
    TextWriter stderr)
{
    private const string NoWork = "No eligible work.";

    public async Task<int> RunAsync(CancellationToken ct)
    {
        try
        {
            if (settings.DryRun)
            {
                await DescribeNextIssueAsync(ct);
            }
            else
            {
                await TakeNextIssueAsync(ct);
            }

            return 0;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            stderr.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception e)
        {
            stderr.WriteLine(e.Message);
            return 1;
        }
    }

    private async Task DescribeNextIssueAsync(CancellationToken ct)
    {
        var issue = await nextIssue.FindAsync(ct);
        stdout.WriteLine(issue is null
            ? NoWork
            : $"Would claim {issue.Number} \"{issue.Title}\" on branch {WorkBranch.For(issue).BranchName}");
    }

    private async Task TakeNextIssueAsync(CancellationToken ct)
    {
        var result = await runOnce.ExecuteAsync(ct);
        if (result.Run is not { } run)
        {
            stdout.WriteLine(NoWork);
            return;
        }

        var runDir = Path.Combine(settings.RunsRoot, run.Id.Value);
        stdout.WriteLine($"Run {run.Id} issue {run.Issue} outcome {run.Outcome} dir {runDir}");
    }
}
