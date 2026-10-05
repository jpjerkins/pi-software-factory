using Factory.Application.Tests.Fakes;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Application.Tests;

public class RunOnceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 4, 15, 30, 0, TimeSpan.Zero);
    private static readonly RunOnceOptions Options =
        new("/clone", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(20));

    private sealed class Harness
    {
        public CallLog Log { get; } = new();
        public FakeClock Clock { get; } = new(Start);
        public FakeIssues Issues { get; }
        public FakeWorktrees Worktrees { get; }
        public FakeFolderTrust Trust { get; }
        public FakeWorkerSlots Slots { get; }
        public FakeUsageProbe Usage { get; }
        public FakeRunStore Store { get; }

        public Harness(params Issue[] open)
        {
            Issues = new FakeIssues(open, Log);
            Worktrees = new FakeWorktrees(Log);
            Trust = new FakeFolderTrust(Log);
            Slots = new FakeWorkerSlots(Log);
            Usage = new FakeUsageProbe(Clock);
            Store = new FakeRunStore(Clock, Log);
        }

        public Task<RunOnceResult> RunAsync() =>
            new RunOnce(Issues, Worktrees, Trust, Slots, Usage, Store, Clock, Options)
                .ExecuteAsync(CancellationToken.None);
    }

    private static Issue Eligible(int number, int createdDay = 1) => new(
        new IssueNumber(number), true, new DateTimeOffset(2026, 9, createdDay, 0, 0, 0, TimeSpan.Zero),
        ["build", "agent:claude"], [], [], []);

    private static WorkerResult Result(ReportedStatus status) => new(status, "s", [], [], [], []);

    [Fact]
    public async Task Nothing_eligible_means_nothing_to_do_and_no_side_effects()
    {
        var ineligible = Eligible(1) with { Assignees = ["someone"] };
        var h = new Harness(ineligible);

        var result = await h.RunAsync();

        Assert.True(result.NothingToDo);
        Assert.Null(result.Run);
        Assert.Empty(h.Log.Calls);
        Assert.Equal(0, h.Usage.Reads);
        Assert.Empty(h.Store.Created);
    }

    [Fact]
    public async Task Picks_the_first_issue_in_pickup_order()
    {
        var h = new Harness(Eligible(5, createdDay: 3), Eligible(7, createdDay: 1));
        h.Store.Result = Result(ReportedStatus.Done);

        await h.RunAsync();

        Assert.Equal(new IssueNumber(7), Assert.Single(h.Issues.Claims).Issue);
    }

    [Theory]
    [InlineData(ReportedStatus.PlanReady, WorkerOutcome.PlanReady)]
    [InlineData(ReportedStatus.Done, WorkerOutcome.Done)]
    [InlineData(ReportedStatus.NeedsInput, WorkerOutcome.NeedsInput)]
    [InlineData(ReportedStatus.Blocked, WorkerOutcome.Blocked)]
    public async Task Reports_the_outcome_the_worker_wrote(ReportedStatus status, WorkerOutcome expected)
    {
        var h = new Harness(Eligible(103));
        h.Store.OnRead = n => { if (n == 3) h.Store.Result = Result(status); };

        var result = await h.RunAsync();

        Assert.False(result.NothingToDo);
        Assert.Equal(expected, result.Run!.Outcome);
        Assert.Equal(3, h.Clock.Waits);
    }

    [Fact]
    public async Task Session_ending_without_a_result_is_crashed()
    {
        var h = new Harness(Eligible(103));
        h.Store.OnRead = n => { if (n == 2) h.Slots.Running = false; };

        var result = await h.RunAsync();

        Assert.Equal(WorkerOutcome.Crashed, result.Run!.Outcome);
    }

    [Fact]
    public async Task Worker_idle_past_the_timeout_is_stuck()
    {
        var h = new Harness(Eligible(103));
        h.Store.Signals = new WorkerSignals(Start, SessionEnded: false);

        var result = await h.RunAsync();

        Assert.Equal(WorkerOutcome.Stuck, result.Run!.Outcome);
        Assert.Equal(Start + TimeSpan.FromMinutes(21), h.Clock.Now);
    }

    [Fact]
    public async Task Claims_before_preparing_the_worktree_and_starting_the_worker()
    {
        var h = new Harness(Eligible(103));
        h.Store.Result = Result(ReportedStatus.Done);

        await h.RunAsync();

        Assert.Equal(
            ["create-run", "claim", "worktree", "trust", "start", "save-run"],
            h.Log.Calls);
    }

    [Fact]
    public async Task Launches_the_worker_in_the_prepared_worktree_and_trusts_the_clone_root()
    {
        var h = new Harness(Eligible(103));
        h.Store.Result = Result(ReportedStatus.Done);

        await h.RunAsync();

        var launch = Assert.Single(h.Slots.Launches);
        Assert.Equal(new IssueNumber(103), launch.Issue);
        Assert.Equal(new Worktree("/clone/wt-103", "lane/103"), launch.Worktree);
        Assert.Equal("20261004-1530-i103", launch.Run.Value);
        Assert.Equal(["/clone"], h.Trust.Trusted);
        Assert.Equal(launch.Run, Assert.Single(h.Issues.Claims).Run);
        Assert.Equal(launch.Run, Assert.Single(h.Store.Created));
    }

    [Fact]
    public async Task Saves_run_with_usage_before_and_after_and_the_outcome()
    {
        var h = new Harness(Eligible(103));
        h.Store.OnRead = n => { if (n == 2) h.Store.Result = Result(ReportedStatus.NeedsInput); };

        var result = await h.RunAsync();

        var saved = Assert.Single(h.Store.Saved);
        Assert.Same(saved, result.Run);
        Assert.Equal(new IssueNumber(103), saved.Issue);
        Assert.Equal(new Worktree("/clone/wt-103", "lane/103"), saved.Worktree);
        Assert.Equal(WorkerOutcome.NeedsInput, saved.Outcome);
        Assert.Equal(Start, saved.StartedAt);
        Assert.Equal(Start + TimeSpan.FromMinutes(2), saved.EndedAt);
        Assert.Equal("usage-1", saved.UsageBefore.RawText);
        Assert.Equal("usage-2", saved.UsageAfter.RawText);
        Assert.Equal(Start, saved.UsageBefore.ReadAt);
    }

    [Fact]
    public async Task Leaves_worktree_and_claim_in_place()
    {
        var h = new Harness(Eligible(103));
        h.Store.Result = Result(ReportedStatus.Done);

        await h.RunAsync();

        Assert.Empty(h.Worktrees.Removed);
        Assert.Empty(h.Store.CleanedUp);
        Assert.Empty(h.Issues.Closed);
        Assert.Empty(h.Issues.Blockers);
        Assert.Empty(h.Issues.Created);
    }
}
