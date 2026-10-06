namespace Factory.Cli.Tests.Hooks;

public class NotificationHookTests
{
    [Fact]
    public void Appends_the_notification_and_prints_nothing()
    {
        using var run = new HookRun().Run("notification",
            """{"notification_type":"idle_prompt","title":"Claude","message":"waiting for you"}""");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Stdout);
        var line = Assert.Single(run.Lines("notifications.log"));
        Assert.Equal("2026-10-05T12:30:00.0000000Z", line.GetProperty("ts").GetString());
        Assert.Equal("idle_prompt", line.GetProperty("type").GetString());
        Assert.Equal("Claude", line.GetProperty("title").GetString());
        Assert.Equal("waiting for you", line.GetProperty("message").GetString());
    }

    [Fact]
    public void Appends_rather_than_overwrites()
    {
        using var run = new HookRun();
        run.Run("notification", """{"message":"a"}""").Run("notification", """{"message":"b"}""");

        Assert.Equal(2, run.Lines("notifications.log").Count);
    }

    [Fact]
    public void Bad_input_exits_0_without_stdout()
    {
        using var run = new HookRun().Run("notification", "not json");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Stdout);
    }
}
