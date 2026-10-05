using Factory.Adapters.Claude;
using Factory.Adapters.Shell;

namespace Factory.Adapters.Tests.Claude;

public class UsageProbeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static UsageProbe Probe(FakeClaudeRunner runner) => new(runner, "claude", new FixedClock(Now));

    [Fact]
    public async Task Read_runs_claude_p_usage_as_an_argument_list()
    {
        var runner = new FakeClaudeRunner(new ProcessResult(0, "Current session: 7% used", ""));

        await Probe(runner).ReadAsync(Ct);

        Assert.Equal("claude", runner.Command);
        Assert.Equal(["-p", "/usage"], runner.Args);
    }

    [Fact]
    public async Task Read_returns_the_sample_output_raw_stamped_with_the_clock()
    {
        var sample = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Claude", "usage-sample.txt"), Ct);
        var runner = new FakeClaudeRunner(new ProcessResult(0, sample, ""));

        var reading = await Probe(runner).ReadAsync(Ct);

        Assert.Equal(sample, reading.RawText);
        Assert.Contains("Current session: 7% used", reading.RawText);
        Assert.Equal(Now, reading.ReadAt);
    }

    [Fact]
    public async Task Read_with_non_zero_exit_throws_with_stderr()
    {
        var runner = new FakeClaudeRunner(new ProcessResult(1, "", "not logged in\n"));

        var ex = await Assert.ThrowsAsync<ClaudeCommandException>(() => Probe(runner).ReadAsync(Ct));

        Assert.Contains("exit 1", ex.Message);
        Assert.Contains("not logged in", ex.Message);
    }

    [Fact]
    public async Task Read_with_empty_output_throws_with_stderr()
    {
        var runner = new FakeClaudeRunner(new ProcessResult(0, "  \n", "some warning"));

        var ex = await Assert.ThrowsAsync<ClaudeCommandException>(() => Probe(runner).ReadAsync(Ct));

        Assert.Contains("no output", ex.Message);
        Assert.Contains("some warning", ex.Message);
    }
}
