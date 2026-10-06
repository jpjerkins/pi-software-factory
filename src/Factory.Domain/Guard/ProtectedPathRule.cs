namespace Factory.Domain.Guard;

/// <summary>
/// Workers may read, but not change, the Claude settings (<c>.claude/</c>, <c>~/.claude.json</c>) and the run
/// directory. The one exception is <c>result.json</c>, where a worker reports its result.
/// </summary>
internal static class ProtectedPathRule
{
    private static readonly HashSet<string> Deleters = ["rm", "rmdir", "unlink", "shred"];

    public static GuardDecision? CheckFileTool(string path, GuardContext context) =>
        IsProtected(path, context.Worktree, context, allowResult: true) ? Deny(path, context) : null;

    public static GuardDecision? CheckCommands(IReadOnlyList<SimpleCommand> commands, GuardContext context)
    {
        string? cwd = context.Worktree;
        foreach (var command in commands)
        {
            cwd = WorkingDirectory.After(command, cwd, context);
            foreach (var (path, allowResult) in WriteTargets(command))
            {
                if (IsProtected(path, cwd, context, allowResult))
                {
                    return Deny(path, context);
                }
            }
        }

        return null;
    }

    // Every path the command would change, and whether result.json is acceptable there.
    private static IEnumerable<(string Path, bool AllowResult)> WriteTargets(SimpleCommand command)
    {
        foreach (var path in Redirections(command.Words))
        {
            yield return (path, true);
        }

        var operands = SimpleCommand.OperandsOf(command.Args).ToList();
        if (Deleters.Contains(command.Name))
        {
            foreach (var path in operands) { yield return (path, false); }
        }
        else if (command.Name == "tee")
        {
            foreach (var path in operands) { yield return (path, true); }
        }
        else if (command.Name == "mv")
        {
            foreach (var path in operands.Take(operands.Count - 1)) { yield return (path, false); }
            foreach (var path in operands.TakeLast(1)) { yield return (path, true); }
        }
        else if (command.Name == "cp" && CopyDestination(command.Args, operands) is { } destination)
        {
            yield return (destination, true);
        }
        else if (command.Name == "sed" && command.Args.Any(IsInPlaceFlag))
        {
            foreach (var path in operands) { yield return (path, false); }
        }
    }

    // Paths after ">" or ">>", whether written "> file", ">file" or "x>file".
    private static IEnumerable<string> Redirections(IReadOnlyList<string> words)
    {
        for (var i = 0; i < words.Count; i++)
        {
            var at = words[i].IndexOf('>');
            if (at < 0)
            {
                continue;
            }

            var rest = words[i][(at + 1)..].TrimStart('>');
            if (rest.Length > 0)
            {
                yield return rest;
            }
            else if (i + 1 < words.Count)
            {
                yield return words[i + 1];
            }
        }
    }

    private static string? CopyDestination(IReadOnlyList<string> args, List<string> operands)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "-t" && i + 1 < args.Count)
            {
                return args[i + 1];
            }

            if (args[i].StartsWith("--target-directory=", StringComparison.Ordinal))
            {
                return args[i]["--target-directory=".Length..];
            }
        }

        return operands.LastOrDefault();
    }

    private static bool IsInPlaceFlag(string arg) =>
        arg.StartsWith("--in-place", StringComparison.Ordinal)
        || (arg.StartsWith('-') && !arg.StartsWith("--") && arg.Contains('i'));

    private static bool IsProtected(string path, string? cwd, GuardContext context, bool allowResult)
    {
        var resolved = PathText.Resolve(path, cwd, context.Home);
        if (resolved is null)
        {
            // Cannot tell where it points, so go by what it says.
            return path.Contains(".claude", StringComparison.Ordinal)
                || path.Contains(context.RunDir, StringComparison.Ordinal);
        }

        if (allowResult && resolved == PathText.Combine(context.RunDir, "result.json"))
        {
            return false;
        }

        return resolved.Split('/').Contains(".claude")
            || PathText.IsUnder(resolved, context.RunDir)
            || resolved == PathText.Combine(context.Home, ".claude.json");
    }

    private static GuardDecision Deny(string path, GuardContext context) =>
        GuardDecision.Deny(
            $"'{path}' is protected: workers may not change .claude directories, ~/.claude.json or the run directory. " +
            $"Write your result to {PathText.Combine(context.RunDir, "result.json")}.");
}
