using System.Text.Json;
using Factory.Adapters.Claude;
using Factory.Adapters.Shell;
using Factory.Application.Ports;
using Factory.Domain.Runs;

namespace Factory.Adapters.Herdr;

/// <summary>
/// Starts each worker as a Claude agent in a fresh tab of the factory workspace.
/// v1 never closes tabs; Phil reads them and cleanup is a later ticket.
/// </summary>
public sealed class HerdrSlots(ICommandRunner runner, HerdrOptions options) : IWorkerSlots
{
    private readonly HerdrCli _herdr = new(runner);

    public async Task<WorkerSession> StartAsync(WorkerLaunch launch, CancellationToken ct)
    {
        var runDir = Path.Combine(options.RunsRoot, launch.Run.Value);
        var settings = await PrepareRunDirAsync(launch, runDir, ct);

        var workspace = await FindOrCreateWorkspaceAsync(launch.Worktree.Path, ct);
        var pane = await CreateTabAsync(workspace, launch, runDir, ct);

        var agentName = $"issue-{launch.Issue.Value}";
        var sessionId = Guid.NewGuid().ToString();
        await _herdr.RunAsync(
        [
            "agent", "start", agentName, "--kind", "claude", "--pane", pane,
            "--timeout", ((int)options.AgentStartTimeout.TotalMilliseconds).ToString(),
            "--",
            "--session-id", sessionId,
            "--model", "sonnet",
            "--permission-mode", "auto",
            "--settings", settings,
            $"Read {Path.Combine(runDir, WorkerPromptFile.FileName)} and follow it.",
        ], ct);

        return new WorkerSession(pane, agentName, sessionId);
    }

    public async Task<bool> IsRunningAsync(WorkerSession session, CancellationToken ct)
    {
        try
        {
            await _herdr.RunAsync(["pane", "get", session.PaneId], ct);
            var agent = await _herdr.RunAsync(["agent", "get", session.AgentName], ct);
            return agent.GetProperty("agent").GetProperty("pane_id").GetString() == session.PaneId;
        }
        catch (HerdrCommandException e) when (e.Code is "pane_not_found" or "agent_not_found")
        {
            return false;
        }
    }

    private async Task<string> PrepareRunDirAsync(WorkerLaunch launch, string runDir, CancellationToken ct)
    {
        Directory.CreateDirectory(runDir);
        Directory.CreateDirectory(GhConfigDir(runDir));
        await new WorkerPromptFile(options.PromptTemplatePath).WriteAsync(runDir, launch.Issue, launch.Worktree.Path, ct);
        return await WorkerSettingsFile.WriteAsync(runDir, options.FactoryBinaryPath, ct);
    }

    private static string GhConfigDir(string runDir) => Path.Combine(runDir, "gh-config");

    private async Task<string> FindOrCreateWorkspaceAsync(string cwd, CancellationToken ct)
    {
        var list = await _herdr.RunAsync(["workspace", "list"], ct);
        foreach (var workspace in list.GetProperty("workspaces").EnumerateArray())
        {
            if (workspace.GetProperty("label").GetString() == options.WorkspaceName)
            {
                return workspace.GetProperty("workspace_id").GetString()!;
            }
        }

        var created = await _herdr.RunAsync(
            ["workspace", "create", "--label", options.WorkspaceName, "--cwd", cwd, "--no-focus"], ct);
        return created.GetProperty("workspace").GetProperty("workspace_id").GetString()!;
    }

    private async Task<string> CreateTabAsync(string workspace, WorkerLaunch launch, string runDir, CancellationToken ct)
    {
        List<string> args =
        [
            "tab", "create", "--workspace", workspace, "--cwd", launch.Worktree.Path,
            "--label", options.SlotLabel, "--no-focus",
        ];
        foreach (var (key, value) in WorkerEnvironment(launch, runDir))
        {
            args.AddRange(["--env", $"{key}={value}"]);
        }

        var tab = await _herdr.RunAsync(args, ct);
        return tab.GetProperty("root_pane").GetProperty("pane_id").GetString()!;
    }

    private IEnumerable<(string Key, string Value)> WorkerEnvironment(WorkerLaunch launch, string runDir)
    {
        foreach (var entry in BaseEnvironment(launch, runDir))
        {
            yield return entry;
        }

        if (!string.IsNullOrEmpty(options.DotnetRoot))
        {
            yield return ("DOTNET_ROOT", options.DotnetRoot);
        }
    }

    private IEnumerable<(string Key, string Value)> BaseEnvironment(WorkerLaunch launch, string runDir) =>
    [
        ("FACTORY_RUN_DIR", runDir),
        ("FACTORY_RUN_ID", launch.Run.Value),
        ("FACTORY_WORKTREE", launch.Worktree.Path),
        ("GH_CONFIG_DIR", GhConfigDir(runDir)),
        ("GIT_CONFIG_GLOBAL", options.GitConfigPath),
        ("Secrets__EnvFile", "/nonexistent/envfile"),
        ("GIT_TERMINAL_PROMPT", "0"),
        ("GH_TOKEN", ""),
        ("GITHUB_TOKEN", ""),
    ];
}
