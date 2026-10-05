namespace Factory.Adapters.Git;

/// <summary>The worktree folder or branch is already there; another session may hold it, so it is never reused or removed.</summary>
public sealed class WorktreeAlreadyExistsException(string message) : Exception(message);
