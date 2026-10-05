using Factory.Domain.Issues;

namespace Factory.Domain.Tests.Issues;

public class IssueOrderingTests
{
    private static readonly DateTimeOffset Jan = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static int[] Order(params Issue[] issues) =>
        [.. new IssueOrdering().Order(issues).Select(i => i.Number.Value)];

    [Fact]
    public void Issues_blocking_more_issues_come_first() =>
        Assert.Equal(
            [3, 2, 1],
            Order(
                AnIssue.Eligible().Number(1).Build(),
                AnIssue.Eligible().Number(2).Blocking(10).Build(),
                AnIssue.Eligible().Number(3).Blocking(10, 11).Build()));

    [Fact]
    public void Equal_block_counts_put_the_oldest_first() =>
        Assert.Equal(
            [2, 3, 1],
            Order(
                AnIssue.Eligible().Number(1).CreatedAt(Jan.AddDays(3)).Build(),
                AnIssue.Eligible().Number(2).CreatedAt(Jan).Build(),
                AnIssue.Eligible().Number(3).CreatedAt(Jan.AddDays(1)).Build()));

    [Fact]
    public void Block_count_beats_age() =>
        Assert.Equal(
            [2, 1],
            Order(
                AnIssue.Eligible().Number(1).CreatedAt(Jan).Build(),
                AnIssue.Eligible().Number(2).CreatedAt(Jan.AddDays(9)).Blocking(10).Build()));

    [Fact]
    public void Ordering_an_empty_list_gives_an_empty_list() =>
        Assert.Empty(new IssueOrdering().Order([]));
}
