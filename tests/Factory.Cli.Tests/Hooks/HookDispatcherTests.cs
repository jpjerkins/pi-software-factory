namespace Factory.Cli.Tests.Hooks;

public class HookDispatcherTests
{
    [Fact]
    public void An_unknown_event_prints_usage_and_exits_2()
    {
        using var run = new HookRun().Run("sleep", "{}");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("Usage", run.Stderr);
        Assert.Equal("", run.Stdout);
    }
}
