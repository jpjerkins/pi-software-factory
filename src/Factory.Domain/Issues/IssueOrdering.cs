namespace Factory.Domain.Issues;

/// <summary>Orders issues for pickup: most directly blocked issues first, then oldest first.</summary>
public sealed class IssueOrdering
{
    public IReadOnlyList<Issue> Order(IEnumerable<Issue> issues) =>
        [.. issues
            .OrderByDescending(i => i.DirectlyBlocks.Count)
            .ThenBy(i => i.CreatedAt)];
}
