using System.Text.RegularExpressions;

namespace Factory.Domain.Guard;

/// <summary>Workers must never reach HERDR: it owns the panes they run in.</summary>
internal static partial class HerdrRule
{
    private const string Advice =
        "Workers must not touch HERDR (the factory owns the panes). Do your work with ordinary tools in the worktree.";

    // The word "herdr" as a command or path segment, not as part of a longer word.
    [GeneratedRegex(@"(^|[\s;&|(`/=""'])herdr($|[\s;&|)`""'])")]
    private static partial Regex HerdrWord();

    public static GuardDecision? CheckCommand(string command) =>
        command.Contains("HERDR_SOCKET_PATH", StringComparison.Ordinal)
        || command.Contains(".config/herdr", StringComparison.Ordinal)
        || HerdrWord().IsMatch(command)
            ? GuardDecision.Deny(Advice)
            : null;

    public static GuardDecision? CheckPath(string absolutePath, GuardContext context)
    {
        var herdrDir = PathText.Combine(context.Home, ".config/herdr");
        return PathText.IsUnder(absolutePath, herdrDir) ? GuardDecision.Deny(Advice) : null;
    }
}
