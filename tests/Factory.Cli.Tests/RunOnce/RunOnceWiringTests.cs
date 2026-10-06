using Factory.Cli.RunOnce;

namespace Factory.Cli.Tests.RunOnce;

public class RunOnceWiringTests
{
    private static readonly RunOnceSettings Settings =
        new("/home/phil/dev/factory/task-guide", "/mnt/data/factory/runs", TimeSpan.FromSeconds(7), TimeSpan.FromMinutes(9), false);

    private static RunOnceWiring Build() => new(Settings, "/opt/factory/factory", "/opt/factory/", "/home/phil", "/home/phil/.dotnet");

    [Fact]
    public void Fixed_values_are_the_task_guide_repo_and_claude()
    {
        Assert.Equal("jpjerkins/task-guide", RunOnceWiring.GitHubRepo);
        Assert.Equal("https://github.com/jpjerkins/task-guide.git", RunOnceWiring.RemoteUrl);
        Assert.Equal("claude", RunOnceWiring.ClaudeCommand);
    }

    [Fact]
    public void Trust_file_is_claude_json_in_home() => Assert.Equal("/home/phil/.claude.json", Build().TrustFilePath);

    [Fact]
    public void Herdr_options_point_at_the_runs_root_binary_and_published_assets()
    {
        var o = Build().HerdrOptions;

        Assert.Equal("/mnt/data/factory/runs", o.RunsRoot);
        Assert.Equal("/opt/factory/factory", o.FactoryBinaryPath);
        Assert.Equal("/opt/factory/assets/worker/gitconfig", o.GitConfigPath);
        Assert.Equal("/opt/factory/assets/worker/prompt.md.template", o.PromptTemplatePath);
    }

    [Fact]
    public void The_dotnet_root_reaches_the_herdr_options() =>
        Assert.Equal("/home/phil/.dotnet", Build().HerdrOptions.DotnetRoot);

    [Fact]
    public void Run_once_options_come_from_the_settings()
    {
        var o = Build().Options;

        Assert.Equal("/home/phil/dev/factory/task-guide", o.RepoRoot);
        Assert.Equal(TimeSpan.FromSeconds(7), o.PollInterval);
        Assert.Equal(TimeSpan.FromMinutes(9), o.StuckTimeout);
    }

    [Fact]
    public void The_use_cases_are_built()
    {
        var w = Build();

        Assert.NotNull(w.RunOnce);
        Assert.NotNull(w.NextIssue);
    }
}
