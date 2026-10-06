namespace Factory.Domain.Guard;

/// <summary>The absolute paths a worker's tool calls are judged against.</summary>
public sealed record GuardContext(string Worktree, string RunDir, string Home);
