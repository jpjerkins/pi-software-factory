using System.Text.Json;
using Factory.Application.Ports;
using Factory.Domain.Runs;

namespace Factory.Adapters.Storage;

/// <summary>Keeps each run in its own directory under a runs root, as files.</summary>
public sealed class RunStore(string runsRoot) : IRunStore
{
    public Task CreateAsync(RunId run, CancellationToken ct)
    {
        var dir = DirOf(run);
        if (Directory.Exists(dir))
        {
            throw new IOException($"Run directory already exists: {dir}");
        }

        Directory.CreateDirectory(dir);
        return Task.CompletedTask;
    }

    public async Task SaveAsync(Run run, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(RunDocument.From(run), StoreJson.Options);
        await File.WriteAllTextAsync(FileOf(run.Id, RunFiles.Run), json, ct);
    }

    public async Task<WorkerResult?> ReadResultAsync(RunId run, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(FileOf(run, RunFiles.Result), ct);
            return JsonSerializer.Deserialize<ResultDocument>(json, StoreJson.Options)?.ToResult();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Missing, or the worker is mid-write: treat as "no result yet".
            return null;
        }
    }

    public Task<WorkerSignals> ReadSignalsAsync(RunId run, CancellationToken ct)
    {
        var activity = new[] { RunFiles.Activity, RunFiles.Notifications, RunFiles.Result }
            .Select(name => FileOf(run, name))
            .Where(File.Exists)
            .Select(path => (DateTimeOffset)File.GetLastWriteTimeUtc(path))
            .ToList();

        // WorkerSignals cannot express "no activity yet", so measure idleness from when the run began.
        var last = activity.Count > 0
            ? activity.Max()
            : (DateTimeOffset)Directory.GetCreationTimeUtc(DirOf(run));

        var ended = File.Exists(FileOf(run, RunFiles.SessionEnded));
        return Task.FromResult(new WorkerSignals(last, ended));
    }

    /// <summary>v1: archive, never delete.</summary>
    public Task CleanUpAsync(RunId run, CancellationToken ct)
    {
        var source = DirOf(run);
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Run directory not found: {source}");
        }

        var archive = Path.Combine(runsRoot, RunFiles.ArchiveDirectory);
        var target = Path.Combine(archive, run.Value);
        if (Directory.Exists(target))
        {
            throw new IOException($"Archive target already exists: {target}");
        }

        Directory.CreateDirectory(archive);
        Directory.Move(source, target);
        return Task.CompletedTask;
    }

    private string DirOf(RunId run) => Path.Combine(runsRoot, run.Value);

    private string FileOf(RunId run, string file) => Path.Combine(DirOf(run), file);
}
