using Factory.Cli.RunOnce;

namespace Factory.Cli.Tests;

public class CommandRouterTests
{
    private sealed class Routed
    {
        public string[]? HookArgs { get; set; }
        public RunOnceSettings? RunOnceSettings { get; set; }
        public StringWriter Stderr { get; } = new();
        public int Exit { get; set; }
    }

    private static async Task<Routed> Route(params string[] args)
    {
        var r = new Routed();
        var router = new CommandRouter(
            a => { r.HookArgs = a; return 7; },
            s => { r.RunOnceSettings = s; return Task.FromResult(8); },
            "/home/phil",
            r.Stderr);
        r.Exit = await router.RunAsync(args);
        return r;
    }

    [Fact]
    public async Task Hook_is_routed_with_all_its_args()
    {
        var r = await Route("hook", "stop");

        Assert.Equal(["hook", "stop"], r.HookArgs!);
        Assert.Equal(7, r.Exit);
        Assert.Null(r.RunOnceSettings);
    }

    [Fact]
    public async Task Run_once_is_routed_with_parsed_settings()
    {
        var r = await Route("run-once", "--dry-run", "--poll-seconds", "4");

        Assert.Equal(8, r.Exit);
        Assert.True(r.RunOnceSettings!.DryRun);
        Assert.Equal(TimeSpan.FromSeconds(4), r.RunOnceSettings.PollInterval);
        Assert.Equal("/home/phil/dev/factory/task-guide", r.RunOnceSettings.RepoRoot);
        Assert.Null(r.HookArgs);
    }

    [Theory]
    [InlineData]
    [InlineData("bogus")]
    public async Task No_or_unknown_command_prints_usage_and_exits_2(params string[] args)
    {
        var r = await Route(args);

        Assert.Equal(2, r.Exit);
        Assert.Contains("Usage: factory hook <event> | factory run-once", r.Stderr.ToString());
        Assert.Null(r.HookArgs);
        Assert.Null(r.RunOnceSettings);
    }

    [Fact]
    public async Task A_bad_run_once_option_prints_the_error_and_usage_and_exits_2()
    {
        var r = await Route("run-once", "--poll-seconds", "x");

        Assert.Equal(2, r.Exit);
        Assert.Contains("--poll-seconds", r.Stderr.ToString());
        Assert.Contains("Usage:", r.Stderr.ToString());
        Assert.Null(r.RunOnceSettings);
    }
}
