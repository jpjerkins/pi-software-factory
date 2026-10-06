using System.Globalization;

namespace Factory.Cli.RunOnce;

/// <summary>Reads the arguments after <c>run-once</c>.</summary>
public static class RunOnceSettingsParser
{
    public static RunOnceSettingsResult Parse(IReadOnlyList<string> args, string home)
    {
        var repoRoot = Path.Combine(home, "dev", "factory", "task-guide");
        var runsRoot = "/mnt/data/factory/runs";
        var poll = TimeSpan.FromSeconds(10);
        var stuck = TimeSpan.FromMinutes(30);
        var dryRun = false;

        for (var i = 0; i < args.Count; i++)
        {
            var option = args[i];
            if (option == "--dry-run")
            {
                dryRun = true;
                continue;
            }

            if (option is not ("--repo-root" or "--runs-root" or "--poll-seconds" or "--stuck-minutes"))
            {
                return Fail($"Unknown option: {option}");
            }

            if (i + 1 >= args.Count)
            {
                return Fail($"Missing value for {option}");
            }

            var value = args[++i];
            switch (option)
            {
                case "--repo-root":
                    repoRoot = value;
                    break;
                case "--runs-root":
                    runsRoot = value;
                    break;
                default:
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
                    {
                        return Fail($"{option} needs a whole number of 1 or more, got: {value}");
                    }

                    if (option == "--poll-seconds")
                    {
                        poll = TimeSpan.FromSeconds(number);
                    }
                    else
                    {
                        stuck = TimeSpan.FromMinutes(number);
                    }

                    break;
            }
        }

        return new RunOnceSettingsResult(new RunOnceSettings(repoRoot, runsRoot, poll, stuck, dryRun), null);
    }

    private static RunOnceSettingsResult Fail(string message) => new(null, message);
}
