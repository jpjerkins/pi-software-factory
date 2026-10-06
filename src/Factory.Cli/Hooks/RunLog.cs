using System.Text.Json;
using Factory.Adapters.Storage;

namespace Factory.Cli.Hooks;

/// <summary>Writes the worker-side files of a run directory: JSON Lines logs and the session-ended marker.</summary>
public sealed class RunLog(string runDir, Func<DateTimeOffset> now)
{
    public void Append(string file, params (string Key, object? Value)[] fields) =>
        File.AppendAllText(Path.Combine(runDir, file), Line(fields) + "\n");

    public void Activity(string hookEvent, string? tool, string decision, string? reason = null)
    {
        var fields = new List<(string, object?)> { ("event", hookEvent), ("tool", tool), ("decision", decision) };
        if (reason is not null)
        {
            fields.Add(("reason", reason));
        }

        Append(RunFiles.Activity, [.. fields]);
    }

    private string Line((string Key, object? Value)[] fields)
    {
        var line = new Dictionary<string, object?> { ["ts"] = now().UtcDateTime.ToString("o") };
        foreach (var (key, value) in fields)
        {
            line[key] = value;
        }

        return JsonSerializer.Serialize(line);
    }
}
