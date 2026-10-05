namespace Factory.Adapters.Git;

/// <summary>A git command exited non-zero. The message carries git's own explanation.</summary>
public sealed class GitCommandException(string command, int exitCode, string stdErr)
    : Exception($"git {command} failed (exit {exitCode}): {stdErr.Trim()}");
