namespace Factory.Domain.Issues;

/// <summary>Decides whether the factory may pick an issue up for an agent.</summary>
public sealed class EligibilityPolicy
{
    public const string BuildLabel = "build";
    public const string ClaudeAgentLabel = "agent:claude";
    public const string RunningLabel = "factory:running";

    public bool IsEligible(Issue issue) =>
        issue.IsOpen
        && issue.HasLabel(BuildLabel)
        && issue.HasLabel(ClaudeAgentLabel)
        && !issue.HasLabel(RunningLabel)
        && !issue.IsAssigned
        && !issue.IsBlocked;
}
