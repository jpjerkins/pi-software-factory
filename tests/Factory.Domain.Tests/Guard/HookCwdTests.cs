using System.Text.Json;
using Factory.Domain.Guard;

namespace Factory.Domain.Tests.Guard;

public class HookCwdTests : ToolGuardTestBase
{
    private static GuardDecision BashIn(string cwd, string command) =>
        ToolGuard.Decide("Bash", Input(("command", command)), Context with { Cwd = cwd });

    [Fact]
    public void Deleting_a_relative_path_is_denied_when_the_cwd_is_outside_the_worktree() =>
        Assert.False(BashIn("/elsewhere", "rm x").IsAllowed);

    [Fact]
    public void Deleting_a_relative_path_is_allowed_when_the_cwd_is_inside_the_worktree() =>
        Assert.True(BashIn(Worktree + "/sub", "rm x").IsAllowed);

    [Fact]
    public void Writing_a_relative_path_into_the_run_dir_is_denied_when_the_cwd_is_the_run_dir() =>
        Assert.False(BashIn(RunDir, "echo hi > run.json").IsAllowed);
}
