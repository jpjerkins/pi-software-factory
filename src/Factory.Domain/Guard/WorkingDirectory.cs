namespace Factory.Domain.Guard;

/// <summary>Follows <c>cd</c> through a command line so later relative paths resolve correctly.</summary>
internal static class WorkingDirectory
{
    /// <returns>The directory after <paramref name="command"/> runs; null when it can no longer be known.</returns>
    public static string? After(SimpleCommand command, string? cwd, GuardContext context)
    {
        switch (command.Name)
        {
            case "cd" or "pushd":
                var dir = command.Args.FirstOrDefault(a => !a.StartsWith('-') || a == "-");
                return dir is null ? context.Home : dir == "-" ? null : PathText.Resolve(dir, cwd, context.Home);
            case "popd":
                return null;
            default:
                return cwd;
        }
    }
}
