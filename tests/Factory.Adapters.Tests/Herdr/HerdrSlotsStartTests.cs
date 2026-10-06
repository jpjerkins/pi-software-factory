using Factory.Adapters.Herdr;
using Factory.Adapters.Tests.Storage;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Adapters.Tests.Herdr;

public sealed class HerdrSlotsStartTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TempRunsRoot _root = new();
    private readonly FakeHerdr _herdr = new();

    public void Dispose() => _root.Dispose();

    private static readonly RunId Run = new("20261004-1530-i103");
    private static readonly WorkerLaunch Launch = new(Run, new IssueNumber(103), new Worktree("/wt/i103", "lane/i103"), "Fix the bug", "Details here");

    private string RunDir => _root.RunDir(Run.Value);

    private HerdrSlots Slots(string? dotnetRoot = null) => new(_herdr, new HerdrOptions(
        RunsRoot: _root.Path,
        FactoryBinaryPath: "/opt/factory/factory",
        GitConfigPath: "/opt/factory/assets/worker/gitconfig",
        PromptTemplatePath: Path.Combine(AppContext.BaseDirectory, "assets", "worker", "prompt.md.template"),
        DotnetRoot: dotnetRoot));

    private HerdrSlots HappyPath()
    {
        _herdr.Replies(FakeHerdr.WorkspaceList).Replies(FakeHerdr.TabCreated).Replies(FakeHerdr.AgentStarted);
        return Slots();
    }

    [Fact]
    public async Task Finds_the_factory_workspace_by_name_then_creates_a_tab_in_it()
    {
        await HappyPath().StartAsync(Launch, Ct);

        Assert.Equal(["workspace", "list"], _herdr.Calls[0]);
        var tab = _herdr.Calls[1];
        Assert.Equal(["tab", "create", "--workspace", "w2", "--cwd", "/wt/i103", "--label", "factory-slot-1", "--no-focus"], tab.Take(9));
    }

    [Fact]
    public async Task The_prompt_carries_the_launch_issue_title_and_body()
    {
        await HappyPath().StartAsync(Launch, Ct);

        var prompt = File.ReadAllText(Path.Combine(RunDir, "prompt.md"));
        Assert.Contains("**#103: Fix the bug**", prompt);
        Assert.Contains("Details here", prompt);
    }

    [Fact]
    public async Task The_tab_gets_exactly_the_worker_environment()
    {
        await HappyPath().StartAsync(Launch, Ct);

        var args = _herdr.Calls[1].Skip(9).ToList();
        Assert.Equal(
        [
            "--env", $"FACTORY_RUN_DIR={RunDir}",
            "--env", "FACTORY_RUN_ID=20261004-1530-i103",
            "--env", "FACTORY_WORKTREE=/wt/i103",
            "--env", $"GH_CONFIG_DIR={RunDir}/gh-config",
            "--env", "GIT_CONFIG_GLOBAL=/opt/factory/assets/worker/gitconfig",
            "--env", "Secrets__EnvFile=/nonexistent/envfile",
            "--env", "GIT_TERMINAL_PROMPT=0",
            "--env", "GH_TOKEN=",
            "--env", "GITHUB_TOKEN=",
        ], args);
    }

    [Fact]
    public async Task The_worker_environment_carries_DOTNET_ROOT_when_set()
    {
        _herdr.Replies(FakeHerdr.WorkspaceList).Replies(FakeHerdr.TabCreated).Replies(FakeHerdr.AgentStarted);

        await Slots("/home/phil/.dotnet").StartAsync(Launch, Ct);

        Assert.Contains("DOTNET_ROOT=/home/phil/.dotnet", _herdr.Calls[1]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task The_worker_environment_has_no_DOTNET_ROOT_when_null_or_empty(string? dotnetRoot)
    {
        _herdr.Replies(FakeHerdr.WorkspaceList).Replies(FakeHerdr.TabCreated).Replies(FakeHerdr.AgentStarted);

        await Slots(dotnetRoot).StartAsync(Launch, Ct);

        Assert.DoesNotContain(_herdr.Calls[1], a => a.StartsWith("DOTNET_ROOT"));
    }

    [Fact]
    public async Task Starts_the_claude_agent_in_the_new_pane_with_the_exact_arguments()
    {
        var session = await HappyPath().StartAsync(Launch, Ct);

        var args = _herdr.Calls[2];
        Assert.Equal(
            ["agent", "start", "issue-103", "--kind", "claude", "--pane", "w2:p5", "--timeout", "60000", "--",
             "--session-id", session.SessionId, "--model", "sonnet", "--permission-mode", "auto",
             "--settings", $"{RunDir}/worker-settings.json", $"Read {RunDir}/prompt.md and follow it."],
            args);
        Assert.Equal(3, _herdr.Calls.Count);
    }

    [Fact]
    public async Task The_session_id_is_a_valid_uuid_and_the_session_records_pane_and_agent()
    {
        var session = await HappyPath().StartAsync(Launch, Ct);

        Assert.True(Guid.TryParseExact(session.SessionId, "D", out _));
        Assert.Equal("w2:p5", session.PaneId);
        Assert.Equal("issue-103", session.AgentName);
    }

    [Fact]
    public async Task Each_start_uses_a_fresh_session_id()
    {
        var first = await HappyPath().StartAsync(Launch, Ct);
        var again = new FakeHerdr().Replies(FakeHerdr.WorkspaceList).Replies(FakeHerdr.TabCreated).Replies(FakeHerdr.AgentStarted);
        var second = await new HerdrSlots(again, new HerdrOptions(_root.Path, "/b", "/g",
            Path.Combine(AppContext.BaseDirectory, "assets", "worker", "prompt.md.template")))
            .StartAsync(Launch, Ct);

        Assert.NotEqual(first.SessionId, second.SessionId);
    }

    [Fact]
    public async Task Writes_settings_prompt_and_an_empty_gh_config_dir_into_the_run_dir()
    {
        await HappyPath().StartAsync(Launch, Ct);

        Assert.Contains("\\\"/opt/factory/factory\\\" hook stop", File.ReadAllText(Path.Combine(RunDir, "worker-settings.json")));
        var prompt = File.ReadAllText(Path.Combine(RunDir, "prompt.md"));
        Assert.Contains("task-guide #103", prompt);
        Assert.Contains("/wt/i103", prompt);
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(RunDir, "gh-config")));
    }

    [Fact]
    public async Task Files_are_written_before_any_herdr_call()
    {
        // The workspace list fails; the run dir must already hold the files the agent will read.
        _herdr.Fails("server_down");

        await Assert.ThrowsAsync<HerdrCommandException>(() => Slots().StartAsync(Launch, Ct));

        Assert.True(File.Exists(Path.Combine(RunDir, "prompt.md")));
    }

    [Fact]
    public async Task Creates_the_factory_workspace_only_when_it_is_missing()
    {
        _herdr.Replies(FakeHerdr.WorkspaceListWithoutFactory).Replies(FakeHerdr.WorkspaceCreated)
            .Replies(FakeHerdr.TabCreated).Replies(FakeHerdr.AgentStarted);

        await Slots().StartAsync(Launch, Ct);

        Assert.Equal(["workspace", "create", "--label", "factory", "--cwd", "/wt/i103", "--no-focus"], _herdr.Calls[1]);
        Assert.Equal("w7", _herdr.Calls[2][3]);
    }

    [Fact]
    public async Task Agent_not_ready_becomes_a_clear_exception()
    {
        _herdr.Replies(FakeHerdr.WorkspaceList).Replies(FakeHerdr.TabCreated)
            .Fails("agent_not_ready", "claude is blocked at the trust dialog");

        var e = await Assert.ThrowsAsync<HerdrCommandException>(() => Slots().StartAsync(Launch, Ct));

        Assert.Equal("agent_not_ready", e.Code);
        Assert.Contains("agent start", e.Message);
        Assert.Contains("trust dialog", e.Message);
    }

    [Fact]
    public async Task A_failed_tab_create_is_reported_and_no_agent_is_started()
    {
        _herdr.Replies(FakeHerdr.WorkspaceList).Fails("workspace_not_found");

        var e = await Assert.ThrowsAsync<HerdrCommandException>(() => Slots().StartAsync(Launch, Ct));

        Assert.Contains("tab create", e.Message);
        Assert.Equal(2, _herdr.Calls.Count);
    }

    [Fact]
    public async Task A_non_json_failure_still_gives_an_exception_with_the_output()
    {
        _herdr.Replies(FakeHerdr.WorkspaceList);
        var fake = new NonJsonFailure(_herdr);

        var e = await Assert.ThrowsAsync<HerdrCommandException>(() => new HerdrSlots(fake, new HerdrOptions(
            _root.Path, "/b", "/g", Path.Combine(AppContext.BaseDirectory, "assets", "worker", "prompt.md.template")))
            .StartAsync(Launch, Ct));

        Assert.Contains("segfault", e.Message);
    }

    private sealed class NonJsonFailure(FakeHerdr inner) : Factory.Adapters.Shell.ICommandRunner
    {
        public Task<Factory.Adapters.Shell.ProcessResult> RunAsync(
            string command, IReadOnlyList<string> args, string? workingDirectory, CancellationToken ct) =>
            args[0] == "workspace" ? inner.RunAsync(command, args, workingDirectory, ct)
                : Task.FromResult(new Factory.Adapters.Shell.ProcessResult(139, "", "segfault"));
    }
}
