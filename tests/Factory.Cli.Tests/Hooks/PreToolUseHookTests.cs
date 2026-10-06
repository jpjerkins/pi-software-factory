namespace Factory.Cli.Tests.Hooks;

public class PreToolUseHookTests
{
    private static string Bash(string command, string cwd = HookRun.Worktree) =>
        HookRun.ToolCall("Bash", new { command }, cwd);

    [Fact]
    public void An_allowed_call_prints_nothing_and_exits_0()
    {
        using var run = new HookRun().Run("pre-tool-use", Bash("ls"));

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Stdout);
    }

    [Fact]
    public void Every_call_is_appended_to_the_activity_log_with_a_utc_timestamp()
    {
        using var run = new HookRun().Run("pre-tool-use", Bash("ls"));

        var line = Assert.Single(run.Lines("activity.log"));
        Assert.Equal("2026-10-05T12:30:00.0000000Z", line.GetProperty("ts").GetString());
        Assert.Equal("pre-tool-use", line.GetProperty("event").GetString());
        Assert.Equal("Bash", line.GetProperty("tool").GetString());
        Assert.Equal("allow", line.GetProperty("decision").GetString());
        Assert.False(run.Exists("denials.log"));
    }

    [Fact]
    public void A_denied_call_prints_the_deny_json_and_exits_0()
    {
        using var run = new HookRun().Run("pre-tool-use", Bash("git push"));

        Assert.Equal(0, run.ExitCode);
        var output = run.Output().GetProperty("hookSpecificOutput");
        Assert.Equal("PreToolUse", output.GetProperty("hookEventName").GetString());
        Assert.Equal("deny", output.GetProperty("permissionDecision").GetString());
        Assert.Contains("git push", output.GetProperty("permissionDecisionReason").GetString());
    }

    [Fact]
    public void A_denied_call_is_logged_to_both_the_denials_and_activity_logs()
    {
        using var run = new HookRun().Run("pre-tool-use", Bash("git push"));

        var denial = Assert.Single(run.Lines("denials.log"));
        Assert.Equal("Bash", denial.GetProperty("tool").GetString());
        Assert.Contains("git push", denial.GetProperty("reason").GetString());
        var activity = Assert.Single(run.Lines("activity.log"));
        Assert.Equal("deny", activity.GetProperty("decision").GetString());
        Assert.Contains("git push", activity.GetProperty("reason").GetString());
    }

    [Fact]
    public void Relative_paths_start_from_the_cwd_in_the_hook_input()
    {
        using var elsewhere = new HookRun().Run("pre-tool-use", Bash("rm x", "/elsewhere"));
        using var inside = new HookRun().Run("pre-tool-use", Bash("rm x", HookRun.Worktree + "/sub"));

        Assert.Contains("deny", elsewhere.Stdout);
        Assert.Equal("", inside.Stdout);
    }

    [Fact]
    public void Calls_are_judged_against_the_home_from_the_environment()
    {
        using var run = new HookRun().Run("pre-tool-use", Bash("cat /home/phil/.config/herdr/x"));

        Assert.Contains("deny", run.Stdout);
    }

    [Theory]
    [InlineData("FACTORY_RUN_DIR")]
    [InlineData("FACTORY_WORKTREE")]
    [InlineData("HOME")]
    public void A_missing_environment_variable_fails_closed(string missing)
    {
        using var run = new HookRun();
        run.Env.Remove(missing);
        run.Run("pre-tool-use", Bash("ls"));

        Assert.Equal(2, run.ExitCode);
        Assert.Equal("deny", run.Output().GetProperty("hookSpecificOutput").GetProperty("permissionDecision").GetString());
        Assert.Contains(missing, run.Stderr);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("[1]")]
    [InlineData("""{"tool_input":{}}""")]
    public void Unusable_stdin_fails_closed(string stdin)
    {
        using var run = new HookRun().Run("pre-tool-use", stdin);

        Assert.Equal(2, run.ExitCode);
        Assert.Equal("deny", run.Output().GetProperty("hookSpecificOutput").GetProperty("permissionDecision").GetString());
        Assert.NotEqual("", run.Stderr);
    }

    [Fact]
    public void An_unexpected_exception_fails_closed()
    {
        using var run = new HookRun();
        Directory.Delete(run.RunDir);
        File.WriteAllText(run.RunDir, "a file where the run directory should be");

        run.Run("pre-tool-use", Bash("ls"));
        File.Delete(run.RunDir);
        Directory.CreateDirectory(run.RunDir);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("deny", run.Stdout);
    }
}
