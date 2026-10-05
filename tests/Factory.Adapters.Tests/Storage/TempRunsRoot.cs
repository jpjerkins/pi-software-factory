namespace Factory.Adapters.Tests.Storage;

/// <summary>A throwaway runs root under the system temp dir, removed on dispose.</summary>
internal sealed class TempRunsRoot : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "factory-tests-" + Guid.NewGuid().ToString("N"));

    public TempRunsRoot() => Directory.CreateDirectory(Path);

    public string RunDir(string runId) => System.IO.Path.Combine(Path, runId);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
