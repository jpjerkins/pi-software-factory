namespace Factory.Cli.RunOnce;

/// <summary>What one <c>factory run-once</c> invocation was asked to do.</summary>
public sealed record RunOnceSettings(
    string RepoRoot,
    string RunsRoot,
    TimeSpan PollInterval,
    TimeSpan StuckTimeout,
    bool DryRun);
