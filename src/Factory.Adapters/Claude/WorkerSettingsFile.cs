using System.Text.Json;

namespace Factory.Adapters.Claude;

/// <summary>
/// Writes the per-run Claude settings that wire the factory's hooks into a worker.
/// Passed with <c>--settings</c>; nothing is ever written into the worktree's <c>.claude/</c>.
/// </summary>
public static class WorkerSettingsFile
{
    public const string FileName = "worker-settings.json";

    private static readonly (string Event, string Kebab, bool MatchesTools)[] Hooks =
    [
        ("PreToolUse", "pre-tool-use", true),
        ("PermissionRequest", "permission-request", false),
        ("Stop", "stop", false),
        ("Notification", "notification", false),
        ("SessionEnd", "session-end", false),
        ("PermissionDenied", "permission-denied", false),
    ];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <returns>The path of the settings file written inside <paramref name="runDir"/>.</returns>
    public static async Task<string> WriteAsync(string runDir, string factoryBinary, CancellationToken ct)
    {
        var hooks = Hooks.ToDictionary(
            h => h.Event,
            h => new[] { Entry(factoryBinary, h.Kebab, h.MatchesTools) });

        var json = JsonSerializer.Serialize(new { hooks }, Options);
        var path = Path.Combine(runDir, FileName);
        await File.WriteAllTextAsync(path, json, ct);
        return path;
    }

    private static Dictionary<string, object> Entry(string binary, string kebab, bool matchesTools)
    {
        var entry = new Dictionary<string, object>();
        if (matchesTools)
        {
            entry["matcher"] = "*";
        }

        entry["hooks"] = new[] { new { type = "command", command = $"\"{binary}\" hook {kebab}" } };
        return entry;
    }
}
