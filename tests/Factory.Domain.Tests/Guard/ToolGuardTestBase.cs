using System.Text.Json;
using Factory.Domain.Guard;

namespace Factory.Domain.Tests.Guard;

/// <summary>Shared fixtures for the ToolGuard rule tests.</summary>
public abstract class ToolGuardTestBase
{
    protected const string Worktree = "/work/repo/wt-1";
    protected const string RunDir = "/data/runs/r1";
    protected const string Home = "/home/phil";

    protected static readonly GuardContext Context = new(Worktree, RunDir, Home);

    protected static GuardDecision Bash(string command) =>
        ToolGuard.Decide("Bash", Input(("command", command)), Context);

    protected static GuardDecision File(string tool, string path) =>
        ToolGuard.Decide(tool, Input(("file_path", path)), Context);

    protected static JsonElement Input(params (string Key, string Value)[] fields) =>
        JsonSerializer.SerializeToElement(fields.ToDictionary(f => f.Key, f => f.Value));
}
