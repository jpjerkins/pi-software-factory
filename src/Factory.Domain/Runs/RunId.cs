using Factory.Domain.Issues;

namespace Factory.Domain.Runs;

/// <summary>Sortable identifier of a run, such as <c>20261004-1530-i103</c>.</summary>
public readonly record struct RunId(string Value)
{
    public static RunId From(DateTimeOffset startedAt, IssueNumber issue) =>
        new($"{startedAt.UtcDateTime:yyyyMMdd-HHmm}-i{issue.Value}");

    public override string ToString() => Value;
}
