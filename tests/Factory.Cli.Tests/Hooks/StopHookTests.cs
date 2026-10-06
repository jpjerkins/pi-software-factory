namespace Factory.Cli.Tests.Hooks;

public class StopHookTests
{
    private const string Stdin = """{"hook_event_name":"Stop","stop_hook_active":false}""";

    private static string Reason(HookRun run) => run.Output().GetProperty("reason").GetString()!;

    [Theory]
    [InlineData("plan_ready")]
    [InlineData("done")]
    [InlineData("needs_input")]
    [InlineData("blocked")]
    public void A_valid_result_lets_the_worker_stop(string status)
    {
        using var run = new HookRun();
        File.WriteAllText(run.PathOf("result.json"), $$"""{"status":"{{status}}","summary":"ok","tests_run":["x"]}""");
        run.Run("stop", Stdin);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Stdout);
        Assert.Equal("allow", Assert.Single(run.Lines("activity.log")).GetProperty("decision").GetString());
    }

    [Fact]
    public void A_missing_result_blocks_and_explains_the_required_shape()
    {
        using var run = new HookRun().Run("stop", Stdin);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("block", run.Output().GetProperty("decision").GetString());
        Assert.Contains("result.json", Reason(run));
        Assert.Contains("does not exist", Reason(run));
        foreach (var name in new[] { "plan_ready", "done", "needs_input", "blocked", "summary", "tests_run", "files_touched", "questions", "problems_found" })
        {
            Assert.Contains(name, Reason(run));
        }
    }

    [Theory]
    [InlineData("{oops", "not valid JSON")]
    [InlineData("""{"status":"finished"}""", "status")]
    [InlineData("{}", "status")]
    public void An_invalid_result_blocks_and_says_what_is_wrong(string content, string expected)
    {
        using var run = new HookRun();
        File.WriteAllText(run.PathOf("result.json"), content);
        run.Run("stop", Stdin);

        Assert.Equal("block", run.Output().GetProperty("decision").GetString());
        Assert.Contains(expected, Reason(run));
        Assert.Equal("block", Assert.Single(run.Lines("activity.log")).GetProperty("decision").GetString());
    }

    [Fact]
    public void Keeps_blocking_even_when_a_stop_hook_is_already_active()
    {
        using var run = new HookRun().Run("stop", """{"stop_hook_active":true}""");

        Assert.Equal("block", run.Output().GetProperty("decision").GetString());
    }

    [Fact]
    public void A_missing_environment_variable_blocks()
    {
        using var run = new HookRun();
        run.Env.Remove("FACTORY_RUN_DIR");
        run.Run("stop", Stdin);

        Assert.Equal("block", run.Output().GetProperty("decision").GetString());
        Assert.Contains("FACTORY_RUN_DIR", Reason(run));
    }
}
