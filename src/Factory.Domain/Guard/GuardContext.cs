namespace Factory.Domain.Guard;

/// <summary>
/// The absolute paths a worker's tool calls are judged against. <paramref name="Cwd"/> is where the worker's
/// shell currently is, when known; relative paths start there instead of at the worktree.
/// </summary>
public sealed record GuardContext(string Worktree, string RunDir, string Home, string? Cwd = null);
