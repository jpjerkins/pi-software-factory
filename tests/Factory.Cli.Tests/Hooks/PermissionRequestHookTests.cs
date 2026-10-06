namespace Factory.Cli.Tests.Hooks;

public class PermissionRequestHookTests
{
    private static readonly string Request = HookRun.ToolCall("Bash", new { command = "make deploy" });

    [Fact]
    public void Always_prints_the_deny_json_and_exits_0()
    {
        using var run = new HookRun().Run("permission-request", Request);

        Assert.Equal(0, run.ExitCode);
        var output = run.Output().GetProperty("hookSpecificOutput");
        Assert.Equal("PermissionRequest", output.GetProperty("hookEventName").GetString());
        var decision = output.GetProperty("decision");
        Assert.Equal("deny", decision.GetProperty("behavior").GetString());
        var message = decision.GetProperty("message").GetString();
        Assert.Contains("unattended", message);
        Assert.Contains("needs_input", message);
        Assert.Contains("blocked", message);
        Assert.Contains("result.json", message);
    }

    [Fact]
    public void Logs_the_request_to_the_denials_and_activity_logs()
    {
        using var run = new HookRun().Run("permission-request", Request);

        var denial = Assert.Single(run.Lines("denials.log"));
        Assert.Equal("Bash", denial.GetProperty("tool").GetString());
        Assert.Equal("permission-request", denial.GetProperty("event").GetString());
        var activity = Assert.Single(run.Lines("activity.log"));
        Assert.Equal("deny", activity.GetProperty("decision").GetString());
        Assert.Equal("permission-request", activity.GetProperty("event").GetString());
    }

    [Fact]
    public void Still_denies_when_the_environment_or_input_is_unusable()
    {
        using var run = new HookRun();
        run.Env.Remove("FACTORY_RUN_DIR");
        run.Run("permission-request", "not json");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("deny", run.Output().GetProperty("hookSpecificOutput").GetProperty("decision").GetProperty("behavior").GetString());
    }
}
