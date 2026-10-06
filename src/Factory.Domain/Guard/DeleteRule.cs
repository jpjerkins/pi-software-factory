namespace Factory.Domain.Guard;

/// <summary>Workers may delete only inside their own worktree. When a target is unclear, deny.</summary>
internal static class DeleteRule
{
    private static readonly HashSet<string> Deleters = ["rm", "rmdir", "unlink", "shred"];
    // Contains '$', so it can never resolve to a path.
    private const string UnknownTarget = "$(operands not visible)";
    private static readonly char[] GlobChars = ['*', '?', '['];

    public static GuardDecision? Check(IReadOnlyList<SimpleCommand> commands, GuardContext context)
    {
        string? cwd = context.Cwd ?? context.Worktree;
        foreach (var command in commands)
        {
            cwd = WorkingDirectory.After(command, cwd, context);
            foreach (var target in Targets(command, cwd, context))
            {
                if (!IsInsideWorktree(target, cwd, context))
                {
                    return GuardDecision.Deny(
                        $"Deleting '{target}' is not allowed: it is not clearly inside your worktree ({context.Worktree}). " +
                        "Delete only with explicit paths inside the worktree.");
                }
            }
        }

        return null;
    }

    // Operands the command would delete. A null entry stands for "could not tell".
    private static IEnumerable<string> Targets(SimpleCommand command, string? cwd, GuardContext context)
    {
        if (Deleters.Contains(command.Name))
        {
            return command.TargetsComeFromInput ? [UnknownTarget] : SimpleCommand.OperandsOf(command.Args);
        }

        if (command.Name == "find" && DeletesFiles(command.Args))
        {
            var paths = command.Args.TakeWhile(a => !a.StartsWith('-') && a is not ("(" or "!")).ToList();
            return paths.Count == 0 ? ["."] : paths;
        }

        if (command.Name == "git")
        {
            return GitCleanTargets(command.Args, cwd, context);
        }

        return [];
    }

    private static bool DeletesFiles(IReadOnlyList<string> args) =>
        args.Contains("-delete")
        || args.Select((a, i) => (a, i)).Any(x =>
            x.a is "-exec" or "-execdir" && x.i + 1 < args.Count && Deleters.Contains(args[x.i + 1]));

    // "git [-C dir] clean [pathspec...]": the directory and any pathspecs are what gets cleaned.
    private static IEnumerable<string> GitCleanTargets(IReadOnlyList<string> args, string? cwd, GuardContext context)
    {
        var dir = ".";
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "-C" && i + 1 < args.Count)
            {
                dir = args[++i];
            }
            else if (!args[i].StartsWith('-'))
            {
                if (args[i] != "clean")
                {
                    yield break;
                }

                var specs = SimpleCommand.OperandsOf(args.Skip(i + 1).ToList()).ToList();
                if (dir != ".")
                {
                    yield return dir;
                }
                else if (cwd is null)
                {
                    yield return UnknownTarget;
                }
                else
                {
                    yield return ".";
                }

                foreach (var spec in specs)
                {
                    yield return dir == "." ? spec : dir.TrimEnd('/') + "/" + spec;
                }

                yield break;
            }
        }
    }

    private static bool IsInsideWorktree(string target, string? cwd, GuardContext context)
    {
        if (target.Contains('{') || target.Contains('$'))
        {
            return false;
        }

        var segments = target.Split('/');
        var firstGlob = Array.FindIndex(segments, s => s.IndexOfAny(GlobChars) >= 0);
        if (firstGlob >= 0)
        {
            // Judge the directory the glob starts in. A glob with no directory, or one that climbs after it, is unclear.
            if (firstGlob == 0 || segments.Skip(firstGlob).Contains(".."))
            {
                return false;
            }

            target = string.Join('/', segments.Take(firstGlob));
            if (target.Length == 0)
            {
                target = "/";
            }
        }

        var resolved = PathText.Resolve(target, cwd, context.Home);
        return resolved is not null && PathText.IsUnder(resolved, context.Worktree);
    }
}
