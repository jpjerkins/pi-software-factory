using Factory.Adapters.GitHub;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Adapters.Tests.GitHub;

public class GitHubIssuesWriteTests
{
    private const string Repo = "jpjerkins/task-guide";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Claim_labels_and_assigns_then_comments_the_run_id()
    {
        var runner = new FakeCommandRunner();

        await new GitHubIssues(Repo, runner).ClaimAsync(new IssueNumber(103), new RunId("20261004-1530-i103"), Ct);

        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal(
            ["issue", "edit", "103", "-R", Repo, "--add-label", "factory:running", "--add-assignee", "@me"],
            runner.Calls[0]);
        Assert.Equal(
            ["issue", "comment", "103", "-R", Repo, "--body", "Claimed by factory run 20261004-1530-i103"],
            runner.Calls[1]);
    }

    [Fact]
    public async Task Claim_that_fails_to_label_does_not_comment()
    {
        var runner = new FakeCommandRunner().Replies("", exitCode: 1, stdErr: "label not found");

        await Assert.ThrowsAsync<GitHubCommandException>(
            () => new GitHubIssues(Repo, runner).ClaimAsync(new IssueNumber(1), new RunId("r"), Ct));

        Assert.Single(runner.Calls);
    }

    [Fact]
    public async Task Close_comments_while_closing()
    {
        var runner = new FakeCommandRunner();

        await new GitHubIssues(Repo, runner).CloseAsync(new IssueNumber(9), "Done in abc123", Ct);

        Assert.Equal(
            [["issue", "close", "9", "-R", Repo, "--comment", "Done in abc123"]],
            runner.Calls);
    }

    [Fact]
    public async Task AddBlocker_looks_up_the_blockers_database_id_then_posts_it_to_the_blocked_issue()
    {
        var runner = new FakeCommandRunner().Replies("5259293904\n");

        await new GitHubIssues(Repo, runner).AddBlockerAsync(new IssueNumber(110), new IssueNumber(49), Ct);

        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal(["api", $"repos/{Repo}/issues/49", "--jq", ".id"], runner.Calls[0]);
        Assert.Equal(
            ["api", "-X", "POST", $"repos/{Repo}/issues/110/dependencies/blocked_by", "-F", "issue_id=5259293904"],
            runner.Calls[1]);
    }

    [Fact]
    public async Task AddBlocker_rejects_an_unreadable_id_without_posting()
    {
        var runner = new FakeCommandRunner().Replies("not-a-number");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new GitHubIssues(Repo, runner).AddBlockerAsync(new IssueNumber(2), new IssueNumber(1), Ct));

        Assert.Single(runner.Calls);
    }

    [Fact]
    public async Task Create_passes_title_body_and_each_label_then_parses_the_number_from_the_url()
    {
        var runner = new FakeCommandRunner().Replies($"https://github.com/{Repo}/issues/207\n");
        var issue = NewIssue.Logged("Odd thing", "Details");

        var number = await new GitHubIssues(Repo, runner).CreateAsync(issue, Ct);

        Assert.Equal(new IssueNumber(207), number);
        Assert.Equal(
            ["issue", "create", "-R", Repo, "--title", "Odd thing", "--body", "Details", "--label", "needs-phil"],
            runner.Calls.Single());
    }

    [Fact]
    public async Task Create_fails_clearly_when_the_output_has_no_issue_url()
    {
        var runner = new FakeCommandRunner().Replies("something odd");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new GitHubIssues(Repo, runner).CreateAsync(NewIssue.Logged("t", "b"), Ct));
    }
}
