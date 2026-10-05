using System.Text.Json;
using System.Text.Json.Nodes;
using Factory.Application.Ports;

namespace Factory.Adapters.Claude;

/// <summary>
/// Marks a folder as trusted in Claude Code's config file (<c>~/.claude.json</c>) so workers skip the trust dialog.
/// Edits via <see cref="JsonNode"/> so every other property survives untouched.
/// </summary>
/// <remarks>
/// The live file is also written by other Claude sessions. We cannot lock against them, so we re-read just
/// before the rename and only commit if the content is unchanged since our read; otherwise we retry once.
/// A tiny window between that check and the rename remains.
/// </remarks>
public sealed class ClaudeTrust(string configPath) : IFolderTrust
{
    private const int Attempts = 2;
    private const string TrustFlag = "hasTrustDialogAccepted";

    public async Task EnsureTrustedAsync(string cloneRoot, CancellationToken ct)
    {
        var key = Normalise(cloneRoot);

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var original = await ReadAsync(ct);
            var root = ParseRoot(original);

            if (!SetTrusted(root, key))
            {
                return;
            }

            var updated = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            if (await TryReplaceAsync(original, updated, ct))
            {
                return;
            }
        }

        throw new IOException($"{configPath} kept changing while trusting {key}; gave up after {Attempts} attempts.");
    }

    private static string Normalise(string cloneRoot)
    {
        var trimmed = cloneRoot.TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed;
    }

    private async Task<string> ReadAsync(CancellationToken ct)
    {
        try
        {
            return await File.ReadAllTextAsync(configPath, ct);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Cannot read Claude config {configPath}: {e.Message}", e);
        }
    }

    private JsonObject ParseRoot(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException($"Claude config {configPath} is not a JSON object.");
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"Claude config {configPath} is not valid JSON: {e.Message}", e);
        }
    }

    /// <returns><c>true</c> if the document changed; <c>false</c> if the folder was already trusted.</returns>
    private bool SetTrusted(JsonObject root, string key)
    {
        if (root["projects"] is not JsonObject projects)
        {
            projects = [];
            root["projects"] = projects;
        }

        if (projects[key] is not JsonObject entry)
        {
            entry = [];
            projects[key] = entry;
        }

        if (entry[TrustFlag] is JsonValue v && v.TryGetValue<bool>(out var trusted) && trusted)
        {
            return false;
        }

        entry[TrustFlag] = true;
        return true;
    }

    /// <returns><c>false</c> if the file changed since <paramref name="original"/> was read (nothing written).</returns>
    private async Task<bool> TryReplaceAsync(string original, string updated, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("ClaudeTrust preserves Unix file permissions and runs on Linux only.");
        }

        var mode = File.GetUnixFileMode(configPath);
        var temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath))!, $".{Path.GetFileName(configPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            // Created with the final mode from the start, so the content is never briefly world-readable.
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = mode };
            await using (var stream = new FileStream(temp, options))
            await using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(updated.AsMemory(), ct);
            }

            File.SetUnixFileMode(temp, mode); // creation is subject to umask

            if (await ReadAsync(ct) != original)
            {
                return false;
            }

            File.Move(temp, configPath, overwrite: true);
            return true;
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
