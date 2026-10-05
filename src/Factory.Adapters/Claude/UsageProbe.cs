using Factory.Adapters.Shell;
using Factory.Application.Ports;
using Factory.Domain.Runs;

namespace Factory.Adapters.Claude;

/// <summary>Reads plan usage by running <c>claude -p "/usage"</c>, which costs no tokens. Keeps the text raw.</summary>
public sealed class UsageProbe(ICommandRunner runner, string claude, IClock clock) : IUsageProbe
{
    private const string Command = "-p /usage";

    public async Task<UsageReading> ReadAsync(CancellationToken ct)
    {
        var result = await runner.RunAsync(claude, ["-p", "/usage"], null, ct);
        if (result.ExitCode != 0)
        {
            throw new ClaudeCommandException(Command, $"failed (exit {result.ExitCode})", result.StdErr);
        }

        if (string.IsNullOrWhiteSpace(result.StdOut))
        {
            throw new ClaudeCommandException(Command, "printed no output", result.StdErr);
        }

        return new UsageReading(result.StdOut, clock.Now);
    }
}
