namespace Factory.Domain.Issues;

/// <summary>
/// A work item as the factory sees it. <paramref name="OpenBlockers"/> are the still-open issues
/// blocking this one; <paramref name="DirectlyBlocks"/> are the open issues this one blocks directly.
/// <paramref name="Body"/> is the issue description as written on GitHub (empty when it has none); optional so
/// callers that do not care about the text need not supply it.
/// </summary>
public sealed record Issue(
    IssueNumber Number,
    string Title,
    bool IsOpen,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<string> Labels,
    IReadOnlyCollection<string> Assignees,
    IReadOnlyCollection<IssueNumber> OpenBlockers,
    IReadOnlyCollection<IssueNumber> DirectlyBlocks,
    string Body = "")
{
    public const string LanePrefix = "lane:";

    public bool HasLabel(string label) => Labels.Contains(label, StringComparer.OrdinalIgnoreCase);

    /// <summary>The lane named by the issue's single <c>lane:*</c> label; null if it has none or several.</summary>
    public string? Lane
    {
        get
        {
            var lanes = Labels
                .Where(l => l.StartsWith(LanePrefix, StringComparison.OrdinalIgnoreCase))
                .Select(l => l[LanePrefix.Length..].ToLowerInvariant())
                .Distinct()
                .ToList();
            return lanes.Count == 1 ? lanes[0] : null;
        }
    }

    public bool IsAssigned => Assignees.Count > 0;

    public bool IsBlocked => OpenBlockers.Count > 0;
}
