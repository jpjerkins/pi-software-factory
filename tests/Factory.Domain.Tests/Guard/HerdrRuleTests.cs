using Factory.Domain.Guard;

namespace Factory.Domain.Tests.Guard;

public class HerdrRuleTests : ToolGuardTestBase
{
    [Theory]
    [InlineData("herdr pane list")]
    [InlineData("command herdr pane list")]
    [InlineData("ls; herdr pane list")]
    [InlineData("echo hi && /usr/local/bin/herdr agent run")]
    [InlineData("X=1 herdr pane list")]
    [InlineData("bash -c 'herdr pane list'")]
    [InlineData("echo $(herdr pane list)")]
    [InlineData("echo $HERDR_SOCKET_PATH")]
    [InlineData("export HERDR_SOCKET_PATH=/tmp/x")]
    [InlineData("ls ~/.config/herdr")]
    [InlineData("cat /home/phil/.config/herdr/herdr.sock")]
    public void Bash_touching_herdr_is_denied(string command)
    {
        var decision = Bash(command);
        Assert.False(decision.IsAllowed);
        Assert.Contains("HERDR", decision.Reason);
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("Write")]
    [InlineData("Edit")]
    [InlineData("MultiEdit")]
    [InlineData("Glob")]
    [InlineData("Grep")]
    public void File_tools_under_herdr_config_are_denied(string tool) =>
        Assert.False(File(tool, "/home/phil/.config/herdr/herdr.sock").IsAllowed);

    [Fact]
    public void Notebook_edit_under_herdr_config_is_denied() =>
        Assert.False(ToolGuard.Decide("NotebookEdit",
            Input(("notebook_path", "/home/phil/.config/herdr/n.ipynb")), Context).IsAllowed);

    [Fact]
    public void Glob_path_field_under_herdr_config_is_denied() =>
        Assert.False(ToolGuard.Decide("Glob",
            Input(("pattern", "*"), ("path", "/home/phil/.config/herdr")), Context).IsAllowed);
}
