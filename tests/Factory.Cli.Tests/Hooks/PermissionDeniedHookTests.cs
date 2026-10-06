namespace Factory.Cli.Tests.Hooks;

public class PermissionDeniedHookTests
{
    [Fact]
    public void Appends_the_auto_mode_denial_and_prints_nothing()
    {
        using var run = new HookRun().Run("permission-denied",
            """{"tool_name":"Bash","tool_input":{"command":"curl x"},"reason":"network access"}""");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Stdout);
        var line = Assert.Single(run.Lines("denials.log"));
        Assert.Equal("2026-10-05T12:30:00.0000000Z", line.GetProperty("ts").GetString());
        Assert.Equal("Bash", line.GetProperty("tool").GetString());
        Assert.Equal("network access", line.GetProperty("reason").GetString());
        Assert.Equal("auto-mode", line.GetProperty("source").GetString());
    }
}
