using Factory.Domain.Guard;

namespace Factory.Domain.Tests.Guard;

public class ProtectedPathRuleTests : ToolGuardTestBase
{
    [Theory]
    [InlineData("Write", "/work/repo/wt-1/.claude/settings.json")]
    [InlineData("Edit", "/work/repo/wt-1/.claude/settings.json")]
    [InlineData("MultiEdit", "/work/repo/wt-1/.claude/settings.json")]
    [InlineData("Write", "/work/repo/wt-1/sub/.claude/CLAUDE.md")]
    [InlineData("Write", "/work/repo/wt-1/sub/../.claude/x")]
    [InlineData("Write", ".claude/settings.json")]
    [InlineData("Write", "/home/phil/.claude/settings.json")]
    [InlineData("Write", "/home/phil/.claude.json")]
    [InlineData("Write", "/data/runs/r1/run.json")]
    [InlineData("Edit", "/data/runs/r1/hooks/pre.sh")]
    [InlineData("Write", "/data/runs/r1/result.json.bak")]
    [InlineData("Write", "/data/runs/r1/sub/result.json")]
    public void Writing_a_protected_path_is_denied(string tool, string path)
    {
        var decision = File(tool, path);
        Assert.False(decision.IsAllowed);
        Assert.Contains("protected", decision.Reason);
    }

    [Theory]
    [InlineData("Write", "/data/runs/r1/result.json")]
    [InlineData("Edit", "/data/runs/r1/result.json")]
    [InlineData("Write", "/data/runs/r10/run.json")]
    [InlineData("Write", "/work/repo/wt-1/claude/x")]
    [InlineData("Write", "/work/repo/wt-1/.claude.json")]
    [InlineData("Write", "/home/phil/.claude.json.txt")]
    [InlineData("Read", "/data/runs/r1/run.json")]
    [InlineData("Read", "/work/repo/wt-1/.claude/settings.json")]
    [InlineData("Read", "/home/phil/.claude.json")]
    [InlineData("Glob", "/home/phil/.claude")]
    [InlineData("Grep", "/data/runs/r1")]
    public void Allowed_file_calls_around_protected_paths(string tool, string path) =>
        Assert.True(File(tool, path).IsAllowed);

    [Fact]
    public void Notebook_edit_in_a_claude_directory_is_denied() =>
        Assert.False(ToolGuard.Decide("NotebookEdit",
            Input(("notebook_path", "/work/repo/wt-1/.claude/n.ipynb")), Context).IsAllowed);

    [Theory]
    [InlineData("file_path")]
    [InlineData("notebook_path")]
    [InlineData("path")]
    public void Unknown_tool_with_a_protected_path_is_denied(string field) =>
        Assert.False(ToolGuard.Decide("Frobnicate",
            Input((field, "/data/runs/r1/run.json")), Context).IsAllowed);

    [Fact]
    public void Unknown_tool_with_a_normal_path_or_none_is_allowed()
    {
        Assert.True(ToolGuard.Decide("Frobnicate", Input(("path", "/work/repo/wt-1/a")), Context).IsAllowed);
        Assert.True(ToolGuard.Decide("Frobnicate", Input(("query", "x")), Context).IsAllowed);
    }

    [Theory]
    [InlineData("echo x > .claude/settings.json")]
    [InlineData("echo x >> /home/phil/.claude/settings.json")]
    [InlineData("echo x>/data/runs/r1/run.json")]
    [InlineData("echo x 2> /data/runs/r1/err.txt")]
    [InlineData("echo x > $HOME/.claude/settings.json")]
    [InlineData("cat a | tee /data/runs/r1/run.json")]
    [InlineData("tee -a .claude/x")]
    [InlineData("mv a .claude/settings.json")]
    [InlineData("mv /data/runs/r1/run.json /work/repo/wt-1/x")]
    [InlineData("cp a /home/phil/.claude.json")]
    [InlineData("cp -r a b /data/runs/r1/")]
    [InlineData("cp -t .claude a")]
    [InlineData("sed -i s/a/b/ .claude/settings.json")]
    [InlineData("sed -i.bak s/a/b/ /home/phil/.claude.json")]
    [InlineData("sed -ni 1p /data/runs/r1/run.json")]
    [InlineData("rm .claude/settings.json")]
    [InlineData("rm -rf .claude")]
    [InlineData("rm /data/runs/r1/result.json")]
    [InlineData("cd .claude && echo x > settings.json")]
    [InlineData("bash -c 'echo x > .claude/s'")]
    public void Bash_writing_a_protected_path_is_denied(string command)
    {
        var decision = Bash(command);
        Assert.False(decision.IsAllowed);
        Assert.Contains("protected", decision.Reason);
    }

    [Theory]
    [InlineData("echo x > /data/runs/r1/result.json")]
    [InlineData("cat /data/runs/r1/run.json")]
    [InlineData("cat .claude/settings.json")]
    [InlineData("ls -la .claude")]
    [InlineData("grep x /home/phil/.claude.json")]
    [InlineData("echo x > out.txt")]
    [InlineData("echo \"a > b\"")]
    [InlineData("dotnet test 2>&1 | tail -5")]
    [InlineData("ls > /dev/null")]
    [InlineData("tee out.txt")]
    [InlineData("cp a b")]
    [InlineData("cp .claude/settings.json copy.json")]
    [InlineData("cp -r /data/runs/r1/run.json copy.json")]
    [InlineData("mv a b")]
    [InlineData("sed -i s/a/b/ src/a.cs")]
    [InlineData("sed -n 1p .claude/settings.json")]
    [InlineData("echo x > $OUT/file")]
    public void Bash_around_protected_paths_is_allowed(string command) =>
        Assert.True(Bash(command).IsAllowed);
}
