using System.Text.Json;

namespace Factory.Cli.Tests.Hooks;

public class SessionEndHookTests
{
    [Fact]
    public void Writes_the_session_ended_marker_and_prints_nothing()
    {
        using var run = new HookRun().Run("session-end", """{"session_id":"abc","reason":"prompt_input_exit"}""");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Stdout);
        var marker = JsonDocument.Parse(File.ReadAllText(run.PathOf("session-ended.json"))).RootElement;
        Assert.Equal("2026-10-05T12:30:00.0000000Z", marker.GetProperty("endedAt").GetString());
        Assert.Equal("prompt_input_exit", marker.GetProperty("reason").GetString());
        Assert.Equal("abc", marker.GetProperty("sessionId").GetString());
    }

    [Fact]
    public void Still_writes_the_marker_when_the_input_is_unusable()
    {
        using var run = new HookRun().Run("session-end", "not json");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Exists("session-ended.json"));
    }
}
