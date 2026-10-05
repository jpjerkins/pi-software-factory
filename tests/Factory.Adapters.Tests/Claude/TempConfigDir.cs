namespace Factory.Adapters.Tests.Claude;

/// <summary>A throwaway directory for synthetic Claude config files, removed on dispose.</summary>
internal sealed class TempConfigDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "factory-trust-tests-" + Guid.NewGuid().ToString("N"));

    public TempConfigDir() => Directory.CreateDirectory(Path);

    public string Write(string json)
    {
        var file = System.IO.Path.Combine(Path, ".claude.json");
        File.WriteAllText(file, json);
        return file;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
