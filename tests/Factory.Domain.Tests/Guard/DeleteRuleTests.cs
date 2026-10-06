namespace Factory.Domain.Tests.Guard;

public class DeleteRuleTests : ToolGuardTestBase
{
    [Theory]
    [InlineData("rm -rf ../other")]
    [InlineData("rm -rf -- ../other")]
    [InlineData("rm /abs/outside")]
    [InlineData("rm /work/repo/wt-10/file")]
    [InlineData("rm -rf /work/repo/wt-1/../wt-2")]
    [InlineData("rm -rf ~/stuff")]
    [InlineData("rm -rf ~")]
    [InlineData("rm -rf ~phil/stuff")]
    [InlineData("rm -rf $HOME/stuff")]
    [InlineData("rm -rf \"$DIR\"")]
    [InlineData("rm -rf /*")]
    [InlineData("rm -rf *")]
    [InlineData("rm -rf *.tmp")]
    [InlineData("rm -rf ../*")]
    [InlineData("rm -rf bin/*/../../..")]
    [InlineData("rm -rf {a,b}")]
    [InlineData("rm -rf bin /abs/outside")]
    [InlineData("cd .. && rm -rf x")]
    [InlineData("cd /tmp; rm x")]
    [InlineData("cd; rm x")]
    [InlineData("cd - && rm x")]
    [InlineData("pushd .. && rm x")]
    [InlineData("rmdir ../x")]
    [InlineData("unlink /etc/hosts")]
    [InlineData("shred -u /abs/file")]
    [InlineData("bash -c 'rm -rf ../x'")]
    [InlineData("echo hi && /bin/rm -rf /abs/x")]
    [InlineData("sudo rm -rf /abs/x")]
    [InlineData("ls | xargs rm")]
    [InlineData("find .. -delete")]
    [InlineData("find / -name '*.o' -delete")]
    [InlineData("find /tmp -type f -exec rm {} +")]
    [InlineData("find $DIR -delete")]
    [InlineData("git -C /work/repo/other clean -fd")]
    [InlineData("git -C .. clean -fdx")]
    [InlineData("git clean -fd ../x")]
    [InlineData("cd .. && git clean -fd")]
    public void Delete_outside_the_worktree_is_denied(string command)
    {
        var decision = Bash(command);
        Assert.False(decision.IsAllowed);
        Assert.Contains("worktree", decision.Reason);
    }

    [Theory]
    [InlineData("rm -rf bin obj")]
    [InlineData("rm -rf ./out")]
    [InlineData("rm -rf bin/*")]
    [InlineData("rm -rf ./*")]
    [InlineData("rm -f /work/repo/wt-1/src/a.cs")]
    [InlineData("rm -rf /work/repo/wt-1/a/../b")]
    [InlineData("rmdir sub")]
    [InlineData("unlink a.txt")]
    [InlineData("cd src && rm a.cs")]
    [InlineData("cd src && cd .. && rm a.cs")]
    [InlineData("cd /work/repo/wt-1/src; rm a.cs")]
    [InlineData("rm -rf")]
    [InlineData("find . -name '*.tmp' -delete")]
    [InlineData("find bin obj -delete")]
    [InlineData("find . -type f -exec rm {} +")]
    [InlineData("find / -name '*.o'")]
    [InlineData("git clean -fd")]
    [InlineData("git -C src clean -fdx")]
    [InlineData("git clean -n")]
    [InlineData("bash -c 'rm -rf bin'")]
    public void Delete_inside_the_worktree_is_allowed(string command) =>
        Assert.True(Bash(command).IsAllowed);
}
