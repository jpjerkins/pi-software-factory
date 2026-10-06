using Factory.Adapters.Herdr;
using Factory.Domain.Runs;

namespace Factory.Adapters.Tests.Herdr;

public sealed class HerdrSlotsIsRunningTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly WorkerSession Session = new("w2:p5", "issue-103", "5b0f3c2e-8f7a-4c55-9a53-1d2e3f4a5b6c");

    private readonly FakeHerdr _herdr = new();

    private Task<bool> IsRunning() =>
        new HerdrSlots(_herdr, new HerdrOptions("/runs", "/b", "/g", "/t")).IsRunningAsync(Session, Ct);

    [Fact]
    public async Task Running_when_the_pane_exists_and_the_agent_is_listed_in_that_pane()
    {
        _herdr.Replies(FakeHerdr.PaneInfo).Replies(FakeHerdr.AgentInfoIn("w2:p5"));

        Assert.True(await IsRunning());
        Assert.Equal(["pane", "get", "w2:p5"], _herdr.Calls[0]);
        Assert.Equal(["agent", "get", "issue-103"], _herdr.Calls[1]);
    }

    [Fact]
    public async Task Not_running_when_the_pane_is_gone_and_the_agent_is_not_even_asked_about()
    {
        _herdr.Fails("pane_not_found");

        Assert.False(await IsRunning());
        Assert.Single(_herdr.Calls);
    }

    [Fact]
    public async Task Not_running_when_claude_has_exited_back_to_the_shell()
    {
        _herdr.Replies(FakeHerdr.PaneInfo).Fails("agent_not_found");

        Assert.False(await IsRunning());
    }

    [Fact]
    public async Task Not_running_when_the_agent_name_now_lives_in_another_pane()
    {
        _herdr.Replies(FakeHerdr.PaneInfo).Replies(FakeHerdr.AgentInfoIn("w2:p9"));

        Assert.False(await IsRunning());
    }

    [Fact]
    public async Task Other_herdr_failures_are_not_mistaken_for_not_running()
    {
        _herdr.Fails("server_down");

        await Assert.ThrowsAsync<HerdrCommandException>(IsRunning);
    }
}
