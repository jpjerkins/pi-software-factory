using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Domain.Tests.Runs;

public class RunIdTests
{
    [Fact]
    public void Is_built_from_utc_time_and_issue_number() =>
        Assert.Equal("20261004-1530-i103",
            RunId.From(new DateTimeOffset(2026, 10, 4, 15, 30, 59, TimeSpan.Zero), new IssueNumber(103)).Value);

    [Fact]
    public void Uses_utc_even_when_given_a_local_offset() =>
        Assert.Equal("20261004-1530-i7",
            RunId.From(new DateTimeOffset(2026, 10, 4, 17, 30, 0, TimeSpan.FromHours(2)), new IssueNumber(7)).Value);

    [Fact]
    public void Later_runs_sort_after_earlier_runs()
    {
        var earlier = RunId.From(new DateTimeOffset(2026, 10, 4, 9, 5, 0, TimeSpan.Zero), new IssueNumber(900));
        var later = RunId.From(new DateTimeOffset(2026, 10, 4, 15, 30, 0, TimeSpan.Zero), new IssueNumber(1));
        Assert.True(string.CompareOrdinal(earlier.Value, later.Value) < 0);
    }
}
