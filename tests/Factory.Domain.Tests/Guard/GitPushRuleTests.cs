namespace Factory.Domain.Tests.Guard;

public class GitPushRuleTests : ToolGuardTestBase
{
    [Theory]
    [InlineData("git push")]
    [InlineData("git push origin main")]
    [InlineData("git -C x push")]
    [InlineData("git --no-pager push")]
    [InlineData("git -c user.name=x push --force")]
    [InlineData("/usr/bin/git push")]
    [InlineData("cd x && git push")]
    [InlineData("false || git push")]
    [InlineData("git status; git push")]
    [InlineData("git log | git push")]
    [InlineData("echo $(git push)")]
    [InlineData("echo `git push`")]
    [InlineData("bash -c 'git push'")]
    [InlineData("sh -c \"cd x && git push origin\"")]
    [InlineData("bash -lc 'git push'")]
    [InlineData("X=1 git push")]
    [InlineData("git status\ngit push")]
    public void Git_push_is_denied(string command)
    {
        var decision = Bash(command);
        Assert.False(decision.IsAllowed);
        Assert.Contains("push", decision.Reason);
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("git commit -m 'push it'")]
    [InlineData("echo git push")]
    [InlineData("git log --grep push")]
    public void Other_git_use_is_allowed(string command) =>
        Assert.True(Bash(command).IsAllowed);
}
