using Factory.Cli.RunOnce;

namespace Factory.Cli.Tests.RunOnce;

public class RunOnceSettingsParserTests
{
    private const string Home = "/home/phil";

    private static RunOnceSettings Ok(params string[] args)
    {
        var result = RunOnceSettingsParser.Parse(args, Home);
        Assert.True(result.IsValid, result.Error);
        return result.Settings!;
    }

    private static string Error(params string[] args)
    {
        var result = RunOnceSettingsParser.Parse(args, Home);
        Assert.False(result.IsValid);
        return result.Error!;
    }

    [Fact]
    public void No_options_gives_the_defaults()
    {
        var s = Ok();

        Assert.Equal("/home/phil/dev/factory/task-guide", s.RepoRoot);
        Assert.Equal("/mnt/data/factory/runs", s.RunsRoot);
        Assert.Equal(TimeSpan.FromSeconds(10), s.PollInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), s.StuckTimeout);
        Assert.False(s.DryRun);
    }

    [Fact]
    public void Repo_root_is_read() => Assert.Equal("/r", Ok("--repo-root", "/r").RepoRoot);

    [Fact]
    public void Runs_root_is_read() => Assert.Equal("/runs", Ok("--runs-root", "/runs").RunsRoot);

    [Fact]
    public void Poll_seconds_is_read() => Assert.Equal(TimeSpan.FromSeconds(3), Ok("--poll-seconds", "3").PollInterval);

    [Fact]
    public void Stuck_minutes_is_read() => Assert.Equal(TimeSpan.FromMinutes(5), Ok("--stuck-minutes", "5").StuckTimeout);

    [Fact]
    public void Dry_run_is_a_flag() => Assert.True(Ok("--dry-run").DryRun);

    [Fact]
    public void Options_combine_in_any_order()
    {
        var s = Ok("--dry-run", "--poll-seconds", "2", "--repo-root", "/r");

        Assert.True(s.DryRun);
        Assert.Equal(TimeSpan.FromSeconds(2), s.PollInterval);
        Assert.Equal("/r", s.RepoRoot);
    }

    [Fact]
    public void An_unknown_option_is_an_error() => Assert.Contains("--bogus", Error("--bogus"));

    [Theory]
    [InlineData("--repo-root")]
    [InlineData("--runs-root")]
    [InlineData("--poll-seconds")]
    [InlineData("--stuck-minutes")]
    public void An_option_without_its_value_is_an_error(string option) => Assert.Contains(option, Error(option));

    [Theory]
    [InlineData("--poll-seconds", "x")]
    [InlineData("--poll-seconds", "1.5")]
    [InlineData("--poll-seconds", "0")]
    [InlineData("--poll-seconds", "-3")]
    [InlineData("--stuck-minutes", "abc")]
    [InlineData("--stuck-minutes", "0")]
    public void A_non_integer_or_too_small_number_is_an_error(string option, string value) =>
        Assert.Contains(option, Error(option, value));
}
