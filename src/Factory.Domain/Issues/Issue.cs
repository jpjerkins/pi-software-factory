namespace Factory.Domain.Issues;

/// <summary>
/// A work item as the factory sees it. <paramref name="OpenBlockers"/> are the still-open issues
/// blocking this one; <paramref name="DirectlyBlocks"/> are the open issues this one blocks directly.
/// </summary>
public sealed record Issue(
    IssueNumber Number,
    bool IsOpen,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<string> Labels,
    IReadOnlyCollection<string> Assignees,
    IReadOnlyCollection<IssueNumber> OpenBlockers,
    IReadOnlyCollection<IssueNumber> DirectlyBlocks)
{
    public bool HasLabel(string label) => Labels.Contains(label, StringComparer.OrdinalIgnoreCase);

    public bool IsAssigned => Assignees.Count > 0;

    public bool IsBlocked => OpenBlockers.Count > 0;
}
