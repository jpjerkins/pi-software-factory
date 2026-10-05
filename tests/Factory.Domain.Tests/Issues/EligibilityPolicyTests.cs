using Factory.Domain.Issues;

namespace Factory.Domain.Tests.Issues;

public class EligibilityPolicyTests
{
    private static bool IsEligible(Issue issue) => new EligibilityPolicy().IsEligible(issue);

    [Fact]
    public void Open_build_agent_claude_unassigned_unblocked_issue_is_eligible() =>
        Assert.True(IsEligible(AnIssue.Eligible().Build()));

    [Fact]
    public void Closed_issue_is_not_eligible() =>
        Assert.False(IsEligible(AnIssue.Eligible().Closed().Build()));

    [Fact]
    public void Issue_without_build_label_is_not_eligible() =>
        Assert.False(IsEligible(AnIssue.Eligible().WithoutLabel("build").Build()));

    [Fact]
    public void Issue_without_agent_claude_label_is_not_eligible() =>
        Assert.False(IsEligible(AnIssue.Eligible().WithoutLabel("agent:claude").Build()));

    [Fact]
    public void Issue_for_another_agent_is_not_eligible() =>
        Assert.False(IsEligible(AnIssue.Eligible().Labelled("build", "agent:codex").Build()));

    [Fact]
    public void Assigned_issue_is_not_eligible() =>
        Assert.False(IsEligible(AnIssue.Eligible().AssignedTo("phil").Build()));

    [Fact]
    public void Issue_labelled_factory_running_is_not_eligible() =>
        Assert.False(IsEligible(AnIssue.Eligible().WithLabel("factory:running").Build()));

    [Fact]
    public void Issue_with_an_open_blocker_is_not_eligible() =>
        Assert.False(IsEligible(AnIssue.Eligible().BlockedByOpen(7).Build()));

    [Fact]
    public void Extra_unrelated_labels_do_not_matter() =>
        Assert.True(IsEligible(AnIssue.Eligible().WithLabel("lane:web-now").Build()));
}
