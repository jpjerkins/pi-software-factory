namespace Factory.Domain.Guard;

/// <summary>Only the factory publishes work. A worker never runs <c>git push</c>.</summary>
internal static class GitPushRule
{
    private static readonly HashSet<string> OptionsWithValue = ["-C", "-c", "--git-dir", "--work-tree", "--namespace"];

    public static GuardDecision? Check(IReadOnlyList<SimpleCommand> commands) =>
        commands.Any(IsPush)
            ? GuardDecision.Deny("Workers must not run git push. Commit in the worktree; the factory publishes the work.")
            : null;

    private static bool IsPush(SimpleCommand command)
    {
        if (command.Name != "git")
        {
            return false;
        }

        for (var i = 0; i < command.Args.Count; i++)
        {
            var arg = command.Args[i];
            if (!arg.StartsWith('-'))
            {
                return arg == "push";
            }

            if (OptionsWithValue.Contains(arg))
            {
                i++;
            }
        }

        return false;
    }
}
