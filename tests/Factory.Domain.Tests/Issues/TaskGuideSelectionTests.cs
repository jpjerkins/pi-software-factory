using Factory.Domain.Issues;

namespace Factory.Domain.Tests.Issues;

public class TaskGuideSelectionTests
{
    [Fact]
    public void Task_guide_snapshot_of_2026_10_04_yields_the_expected_pickup_order()
    {
        var issues = TaskGuideIssueFixture.Load("task-guide-issues-2026-10-04.json");
        var policy = new EligibilityPolicy();

        var order = new IssueOrdering()
            .Order(issues.Where(policy.IsEligible))
            .Select(i => i.Number.Value);

        // #185 was created after the spec's list was written (2026-10-05 UTC), so it queues last.
        Assert.Equal([103, 106, 105, 182, 185], order);
    }
}
