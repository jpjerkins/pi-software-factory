namespace Factory.Domain.Guard;

/// <summary>Pure path arithmetic on POSIX-style text. Never touches the file system.</summary>
internal static class PathText
{
    public static string Combine(string directory, string relative) =>
        directory.TrimEnd('/') + "/" + relative;

    public static bool IsUnder(string path, string directory)
    {
        var root = directory.TrimEnd('/');
        return path == root || path.StartsWith(root + "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Makes <paramref name="target"/> absolute and normalised (<c>~</c> is home, relative is under
    /// <paramref name="cwd"/>), or returns null when it cannot be known without a shell (<c>$VAR</c>, <c>~user</c>).
    /// </summary>
    public static string? Resolve(string target, string? cwd, string home)
    {
        if (target.Contains('$') || target.Contains('`'))
        {
            return null;
        }

        string? full;
        if (target == "~" || target.StartsWith("~/", StringComparison.Ordinal))
        {
            full = home + target[1..];
        }
        else if (target.StartsWith('~'))
        {
            return null;
        }
        else
        {
            full = target.StartsWith('/') ? target : cwd is null ? null : cwd + "/" + target;
        }

        return full is null ? null : Normalise(full);
    }

    private static string Normalise(string absolute)
    {
        var parts = new List<string>();
        foreach (var part in absolute.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                if (parts.Count > 0) { parts.RemoveAt(parts.Count - 1); }
            }
            else if (part != ".")
            {
                parts.Add(part);
            }
        }

        return "/" + string.Join('/', parts);
    }
}
