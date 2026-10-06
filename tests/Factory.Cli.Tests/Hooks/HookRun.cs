using System.Text.Json;
using Factory.Cli.Hooks;

namespace Factory.Cli.Tests.Hooks;

/// <summary>Runs one hook in-process against a temp run directory with fake stdin, env and clock.</summary>
public sealed class HookRun : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 30, 0, TimeSpan.Zero);
    public const string Worktree = "/work/repo/wt-1";
    public const string Home = "/home/phil";

    public string RunDir { get; } = Directory.CreateTempSubdirectory("hook-").FullName;
    public Dictionary<string, string> Env { get; }
    public string Stdout { get; private set; } = "";
    public string Stderr { get; private set; } = "";
    public int ExitCode { get; private set; }

    public HookRun()
    {
        Env = new()
        {
            ["FACTORY_RUN_DIR"] = RunDir,
            ["FACTORY_RUN_ID"] = "r1",
            ["FACTORY_WORKTREE"] = Worktree,
            ["HOME"] = Home,
        };
    }

    public void Dispose() => Directory.Delete(RunDir, recursive: true);

    public HookRun Run(string hookEvent, string stdin)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var io = new HookIo(new StringReader(stdin), stdout, stderr, name => Env.GetValueOrDefault(name), () => Now);
        ExitCode = HookDispatcher.Run(["hook", hookEvent], io);
        Stdout = stdout.ToString();
        Stderr = stderr.ToString();
        return this;
    }

    public string PathOf(string file) => Path.Combine(RunDir, file);

    public bool Exists(string file) => File.Exists(PathOf(file));

    public List<JsonElement> Lines(string file) =>
        File.ReadAllLines(PathOf(file)).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    public JsonElement Output() => JsonDocument.Parse(Stdout).RootElement;

    public static string ToolCall(string tool, object input, string cwd = Worktree) =>
        JsonSerializer.Serialize(new { tool_name = tool, tool_input = input, cwd });
}
