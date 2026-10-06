namespace Factory.Application;

/// <param name="RepoRoot">
/// The repo's folder holding <c>main/</c> and every issue worktree.
/// </param>
/// <param name="PollInterval">How often to look for a worker outcome.</param>
/// <param name="StuckTimeout">How long without progress before a worker counts as stuck.</param>
public sealed record RunOnceOptions(string RepoRoot, TimeSpan PollInterval, TimeSpan StuckTimeout);
