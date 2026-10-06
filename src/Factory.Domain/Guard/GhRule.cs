namespace Factory.Domain.Guard;

/// <summary>Only the factory talks to GitHub. A worker never runs <c>gh</c>.</summary>
internal static class GhRule
{
    public static GuardDecision? Check(IReadOnlyList<SimpleCommand> commands) =>
        commands.Any(c => c.Name == "gh")
            ? GuardDecision.Deny("Workers must not run gh. The factory handles GitHub; report what you need in result.json.")
            : null;
}
