using Factory.Adapters.Claude;
using Factory.Adapters.Git;
using Factory.Adapters.GitHub;
using Factory.Adapters.Herdr;
using Factory.Adapters.Shell;
using Factory.Adapters.Storage;
using Factory.Adapters.Time;
using Factory.Application;

namespace Factory.Cli.RunOnce;

/// <summary>Composition root for <c>run-once</c>: builds the real use cases from the settings. Does no I/O.</summary>
public sealed class RunOnceWiring
{
    public const string GitHubRepo = "jpjerkins/task-guide";
    public const string RemoteUrl = "https://github.com/jpjerkins/task-guide.git";
    public const string ClaudeCommand = "claude";

    public RunOnceWiring(RunOnceSettings settings, string factoryBinaryPath, string baseDirectory, string home, string? dotnetRoot)
    {
        TrustFilePath = Path.Combine(home, ".claude.json");
        HerdrOptions = new HerdrOptions(
            RunsRoot: settings.RunsRoot,
            FactoryBinaryPath: factoryBinaryPath,
            GitConfigPath: Path.Combine(baseDirectory, "assets", "worker", "gitconfig"),
            PromptTemplatePath: Path.Combine(baseDirectory, "assets", "worker", "prompt.md.template"),
            DotnetRoot: dotnetRoot);
        Options = new RunOnceOptions(settings.RepoRoot, settings.PollInterval, settings.StuckTimeout);

        var runner = new ProcessRunner();
        var clock = new SystemClock();
        var issues = new GitHubIssues(GitHubRepo, runner);
        RunOnce = new Factory.Application.RunOnce(
            issues,
            new GitWorktrees(settings.RepoRoot, RemoteUrl),
            new ClaudeTrust(TrustFilePath),
            new HerdrSlots(runner, HerdrOptions),
            new UsageProbe(runner, ClaudeCommand, clock),
            new RunStore(settings.RunsRoot),
            clock,
            Options);
        NextIssue = new NextIssue(issues);
    }

    public string TrustFilePath { get; }

    public HerdrOptions HerdrOptions { get; }

    public RunOnceOptions Options { get; }

    public Factory.Application.RunOnce RunOnce { get; }

    public NextIssue NextIssue { get; }
}
