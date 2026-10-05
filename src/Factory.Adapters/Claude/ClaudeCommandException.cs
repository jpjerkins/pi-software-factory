namespace Factory.Adapters.Claude;

/// <summary>A claude command failed or printed nothing. The message carries claude's stderr.</summary>
public sealed class ClaudeCommandException(string command, string problem, string stdErr)
    : Exception($"claude {command} {problem}: {stdErr.Trim()}");
