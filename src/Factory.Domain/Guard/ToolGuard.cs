using System.Text.Json;

namespace Factory.Domain.Guard;

/// <summary>Decides whether a Claude Code worker's tool call is allowed. Pure: no I/O.</summary>
public static class ToolGuard
{
    private static readonly string[] PathFields = ["file_path", "notebook_path", "path"];
    private static readonly HashSet<string> ReadOnlyTools = ["Read", "Glob", "Grep"];

    public static GuardDecision Decide(string toolName, JsonElement toolInput, GuardContext context)
    {
        if (toolName == "Bash")
        {
            return Text(toolInput, "command") is { } command ? DecideBash(command, context) : GuardDecision.Allow;
        }

        foreach (var field in PathFields)
        {
            if (Text(toolInput, field) is not { } path)
            {
                continue;
            }

            var denial = HerdrRule.CheckPath(path, context)
                ?? (ReadOnlyTools.Contains(toolName) ? null : ProtectedPathRule.CheckFileTool(path, context));
            if (denial is not null)
            {
                return denial;
            }
        }

        return GuardDecision.Allow;
    }

    private static GuardDecision DecideBash(string command, GuardContext context)
    {
        var commands = ShellCommand.Parse(command);
        return HerdrRule.CheckCommand(command)
            ?? GitPushRule.Check(commands)
            ?? GhRule.Check(commands)
            ?? ProtectedPathRule.CheckCommands(commands, context)
            ?? DeleteRule.Check(commands, context)
            ?? GuardDecision.Allow;
    }

    private static string? Text(JsonElement input, string field) =>
        input.ValueKind == JsonValueKind.Object
        && input.TryGetProperty(field, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
