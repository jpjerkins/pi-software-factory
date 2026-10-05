using Factory.Domain.Issues;

namespace Factory.Domain.Tests.Issues;

public class NewIssueTests
{
    [Fact]
    public void Factory_logged_issue_has_exactly_the_needs_phil_label() =>
        Assert.Equal(["needs-phil"], NewIssue.Logged("t", "b").Labels);

    [Fact]
    public void Factory_logged_issue_never_gets_build_or_agent_labels()
    {
        var labels = NewIssue.Logged("t", "b").Labels;
        Assert.DoesNotContain(EligibilityPolicy.BuildLabel, labels);
        Assert.DoesNotContain(labels, l => l.StartsWith("agent:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Keeps_title_and_body()
    {
        var issue = NewIssue.Logged("a title", "a body");
        Assert.Equal("a title", issue.Title);
        Assert.Equal("a body", issue.Body);
    }
}
