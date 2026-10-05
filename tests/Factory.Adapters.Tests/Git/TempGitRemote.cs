using Factory.Adapters.Shell;

namespace Factory.Adapters.Tests.Git;

/// <summary>
/// A throwaway local bare repo standing in for the remote, with one commit on main, plus a repo-root folder
/// next to it. Everything lives under one temp directory and is removed on dispose. No network.
/// </summary>
internal sealed class TempGitRemote : IDisposable
{
    private static readonly string[] Identity = ["-c", "user.name=Test", "-c", "user.email=test@example.invalid"];

    private readonly string _base = Path.Combine(Path.GetTempPath(), "factory-git-tests-" + Guid.NewGuid().ToString("N"));

    public TempGitRemote()
    {
        Directory.CreateDirectory(_base);
        Git(_base, "init", "--bare", "-b", "main", RemoteUrl);
        var seed = Path.Combine(_base, "seed");
        Git(_base, "clone", RemoteUrl, seed);
        File.WriteAllText(Path.Combine(seed, "README.md"), "hello");
        Git(seed, "add", ".");
        Git(seed, [.. Identity, "commit", "-m", "initial"]);
        Git(seed, "push", "origin", "HEAD:main");
    }

    public string RemoteUrl => Path.Combine(_base, "remote.git");

    /// <summary>Where the factory's repo root goes; does not exist until something creates it.</summary>
    public string RepoRoot => Path.Combine(_base, "repo-root");

    public string Main => Path.Combine(RepoRoot, "main");

    public static string Run(string directory, params string[] args) => Git(directory, args);

    public static string Commit(string directory, string fileName)
    {
        File.WriteAllText(Path.Combine(directory, fileName), "x");
        Git(directory, "add", ".");
        return Git(directory, [.. Identity, "commit", "-m", "add " + fileName]);
    }

    public void Dispose() => Directory.Delete(_base, recursive: true);

    private static string Git(string directory, params string[] args)
    {
        var result = new ProcessRunner().RunAsync("git", args, directory, CancellationToken.None).GetAwaiter().GetResult();
        return result.ExitCode == 0
            ? result.StdOut
            : throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {result.StdErr}");
    }
}
