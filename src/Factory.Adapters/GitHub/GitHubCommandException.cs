namespace Factory.Adapters.GitHub;

/// <summary>A gh command exited non-zero. The message carries gh's own explanation.</summary>
public sealed class GitHubCommandException(string command, int exitCode, string stdErr)
    : Exception($"gh {command} failed (exit {exitCode}): {stdErr.Trim()}");
