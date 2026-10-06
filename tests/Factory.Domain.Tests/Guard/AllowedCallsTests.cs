namespace Factory.Domain.Tests.Guard;

public class AllowedCallsTests : ToolGuardTestBase
{
    [Theory]
    [InlineData("dotnet test")]
    [InlineData("git status")]
    [InlineData("git commit -m 'fix'")]
    [InlineData("echo ghost")]
    [InlineData("ls /usr/share/ghc/bin")]
    [InlineData("rm -rf bin obj")]
    [InlineData("rm /work/repo/wt-1/tmp.txt")]
    public void Ordinary_bash_is_allowed(string command) =>
        Assert.True(Bash(command).IsAllowed);

    [Theory]
    [InlineData("Read", "/work/repo/wt-1/src/a.cs")]
    [InlineData("Write", "/work/repo/wt-1/src/a.cs")]
    [InlineData("Edit", "/work/repo/wt-1/src/a.cs")]
    [InlineData("Read", "/data/runs/r1/prompt.md")]
    [InlineData("Read", "/work/repo/wt-1/.claude/settings.json")]
    [InlineData("Read", "/home/phil/.claude.json")]
    public void Ordinary_file_calls_are_allowed(string tool, string path) =>
        Assert.True(File(tool, path).IsAllowed);
}
