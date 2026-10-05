using Factory.Domain.Runs;

namespace Factory.Domain.Tests.Runs;

public class OutcomeDetectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(20);

    private static WorkerResult Result(ReportedStatus status) => new(status, "s", [], [], [], []);

    private static WorkerSignals Signals(TimeSpan idle, bool ended = false) => new(Now - idle, ended);

    private static WorkerOutcome? Detect(WorkerResult? result, WorkerSignals signals, bool isRunning = true) =>
        OutcomeDetection.Detect(result, signals, isRunning, Now, Timeout);

    [Theory]
    [InlineData(ReportedStatus.PlanReady, WorkerOutcome.PlanReady)]
    [InlineData(ReportedStatus.Done, WorkerOutcome.Done)]
    [InlineData(ReportedStatus.NeedsInput, WorkerOutcome.NeedsInput)]
    [InlineData(ReportedStatus.Blocked, WorkerOutcome.Blocked)]
    public void Result_present_gives_its_status(ReportedStatus status, WorkerOutcome expected) =>
        Assert.Equal(expected, Detect(Result(status), Signals(TimeSpan.Zero)));

    [Fact]
    public void Result_wins_even_if_session_ended_and_idle() =>
        Assert.Equal(WorkerOutcome.Done,
            Detect(Result(ReportedStatus.Done), Signals(TimeSpan.FromHours(5), ended: true), isRunning: false));

    [Fact]
    public void Session_ended_signal_without_result_is_crashed() =>
        Assert.Equal(WorkerOutcome.Crashed, Detect(null, Signals(TimeSpan.Zero, ended: true)));

    [Fact]
    public void Not_running_without_result_is_crashed() =>
        Assert.Equal(WorkerOutcome.Crashed, Detect(null, Signals(TimeSpan.Zero), isRunning: false));

    [Fact]
    public void Crashed_wins_over_stuck() =>
        Assert.Equal(WorkerOutcome.Crashed, Detect(null, Signals(TimeSpan.FromHours(1)), isRunning: false));

    [Fact]
    public void Idle_longer_than_timeout_is_stuck() =>
        Assert.Equal(WorkerOutcome.Stuck, Detect(null, Signals(Timeout + TimeSpan.FromSeconds(1))));

    [Fact]
    public void Idle_exactly_the_timeout_keeps_waiting() =>
        Assert.Null(Detect(null, Signals(Timeout)));

    [Fact]
    public void Recent_activity_keeps_waiting() =>
        Assert.Null(Detect(null, Signals(TimeSpan.FromMinutes(1))));
}
