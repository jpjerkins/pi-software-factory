namespace Factory.Domain.Tests.Guard;

public class GhRuleTests : ToolGuardTestBase
{
    [Theory]
    [InlineData("gh pr list")]
    [InlineData("X=1 gh pr list")]
    [InlineData("/usr/bin/gh pr list")]
    [InlineData("command gh issue view 1")]
    [InlineData("ls && gh api /user")]
    [InlineData("ls; gh auth status")]
    [InlineData("cat x | gh issue create")]
    [InlineData("echo $(gh pr list)")]
    [InlineData("echo `gh pr list`")]
    [InlineData("bash -c 'gh pr list'")]
    [InlineData("sh -c \"echo hi; gh pr list\"")]
    [InlineData("env GH_TOKEN=x gh pr list")]
    [InlineData("gh")]
    public void Gh_command_is_denied(string command)
    {
        var decision = Bash(command);
        Assert.False(decision.IsAllowed);
        Assert.Contains("gh", decision.Reason);
    }

    [Theory]
    [InlineData("echo ghost")]
    [InlineData("ls /opt/gh/bin")]
    [InlineData("cat ~/gh/notes.txt")]
    [InlineData("git log --author=gh")]
    [InlineData("dotnet test --filter gh")]
    public void Gh_as_a_substring_or_argument_is_allowed(string command) =>
        Assert.True(Bash(command).IsAllowed);
}
