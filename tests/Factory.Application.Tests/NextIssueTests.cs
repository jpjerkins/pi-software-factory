using Factory.Application.Tests.Fakes;
using Factory.Domain.Issues;

namespace Factory.Application.Tests;

public class NextIssueTests
{
    private static Issue Eligible(int number, int createdDay = 1) => new(
        new IssueNumber(number), $"Issue {number}", true, new DateTimeOffset(2026, 9, createdDay, 0, 0, 0, TimeSpan.Zero),
        ["build", "agent:claude", "lane:adapters"], [], [], []);

    [Fact]
    public async Task No_eligible_issue_gives_null()
    {
        var ineligible = Eligible(1) with { Assignees = ["someone"] };
        var issues = new FakeIssues([ineligible], new CallLog());

        var next = await new NextIssue(issues).FindAsync(CancellationToken.None);

        Assert.Null(next);
    }

    [Fact]
    public async Task Picks_the_top_ordered_eligible_issue()
    {
        var issues = new FakeIssues([Eligible(5, createdDay: 3), Eligible(7, createdDay: 1)], new CallLog());

        var next = await new NextIssue(issues).FindAsync(CancellationToken.None);

        Assert.Equal(new IssueNumber(7), next!.Number);
    }

    [Fact]
    public async Task Makes_no_writes()
    {
        var log = new CallLog();
        var issues = new FakeIssues([Eligible(1)], log);

        await new NextIssue(issues).FindAsync(CancellationToken.None);

        Assert.Empty(issues.Claims);
        Assert.Empty(log.Calls);
    }
}
