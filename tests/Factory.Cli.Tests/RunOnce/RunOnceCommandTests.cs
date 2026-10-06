using Factory.Cli.RunOnce;
using Factory.Cli.Tests.RunOnce.Fakes;

namespace Factory.Cli.Tests.RunOnce;

public class RunOnceCommandTests
{
    private static RunOnceSettings Settings(bool dryRun) =>
        new("/repo", "/runs", TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(30), dryRun);

    private sealed record Outcome(int Exit, string Out, string Err);

    private static async Task<Outcome> Run(FakeFactory f, bool dryRun, CancellationToken? token = null)
    {
        var ct = token ?? TestContext.Current.CancellationToken;
        var o = new StringWriter();
        var e = new StringWriter();
        var exit = await new RunOnceCommand(Settings(dryRun), f.RunOnce, f.NextIssue, o, e).RunAsync(ct);
        return new Outcome(exit, o.ToString(), e.ToString());
    }

    [Fact]
    public async Task Dry_run_says_what_it_would_claim_and_touches_nothing()
    {
        var f = new FakeFactory(FakeFactory.Eligible(12, "Add the thing"));

        var r = await Run(f, dryRun: true);

        Assert.Equal(0, r.Exit);
        Assert.Equal("Would claim #12 \"Add the thing\" on branch feat/12-add-the-thing\n", r.Out.Replace("\r\n", "\n"));
        Assert.True(f.TouchedNothing);
    }

    [Fact]
    public async Task Dry_run_with_no_eligible_work_says_so_and_touches_nothing()
    {
        var f = new FakeFactory();

        var r = await Run(f, dryRun: true);

        Assert.Equal(0, r.Exit);
        Assert.Equal("No eligible work.", r.Out.Trim());
        Assert.True(f.TouchedNothing);
    }

    [Fact]
    public async Task A_normal_run_prints_one_line_with_run_issue_outcome_and_dir()
    {
        var f = new FakeFactory(FakeFactory.Eligible(12, "Add the thing"));

        var r = await Run(f, dryRun: false);

        Assert.Equal(0, r.Exit);
        var line = Assert.Single(r.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("20261005-1230-i12", line);
        Assert.Contains("#12", line);
        Assert.Contains("Done", line);
        Assert.Contains("/runs/20261005-1230-i12", line);
        Assert.Single(f.Issues.Claimed);
        Assert.Equal(1, f.Slots.Started);
    }

    [Fact]
    public async Task A_normal_run_with_no_work_says_so()
    {
        var f = new FakeFactory();

        var r = await Run(f, dryRun: false);

        Assert.Equal(0, r.Exit);
        Assert.Equal("No eligible work.", r.Out.Trim());
        Assert.True(f.TouchedNothing);
    }

    [Fact]
    public async Task A_failure_goes_to_stderr_with_exit_1()
    {
        var f = new FakeFactory(new FakeIssues { FailWith = new InvalidOperationException("gh exploded") });

        var r = await Run(f, dryRun: false);

        Assert.Equal(1, r.Exit);
        Assert.Contains("gh exploded", r.Err);
        Assert.Equal("", r.Out);
    }

    [Fact]
    public async Task Cancellation_says_cancelled_with_exit_130()
    {
        var f = new FakeFactory(FakeFactory.Eligible(12, "Add the thing"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var r = await Run(f, dryRun: false, cts.Token);

        Assert.Equal(130, r.Exit);
        Assert.Contains("Cancelled.", r.Err);
        Assert.Equal("", r.Out);
    }
}
