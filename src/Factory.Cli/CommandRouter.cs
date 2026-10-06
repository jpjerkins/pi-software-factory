using Factory.Cli.RunOnce;

namespace Factory.Cli;

/// <summary>Routes the command line to the <c>hook</c> or <c>run-once</c> handler.</summary>
public sealed class CommandRouter(
    Func<string[], int> hook,
    Func<RunOnceSettings, Task<int>> runOnce,
    string home,
    TextWriter stderr)
{
    public const string Usage =
        "Usage: factory hook <event> | factory run-once [--dry-run] [--repo-root <path>] [--runs-root <path>] [--poll-seconds <n>] [--stuck-minutes <n>]";

    public Task<int> RunAsync(string[] args)
    {
        switch (args)
        {
            case ["hook", ..]:
                return Task.FromResult(hook(args));
            case ["run-once", .. var options]:
                var parsed = RunOnceSettingsParser.Parse(options, home);
                return parsed.Settings is { } settings ? runOnce(settings) : Fail(parsed.Error);
            default:
                return Fail(null);
        }
    }

    private Task<int> Fail(string? error)
    {
        if (error is not null)
        {
            stderr.WriteLine(error);
        }

        stderr.WriteLine(Usage);
        return Task.FromResult(2);
    }
}
