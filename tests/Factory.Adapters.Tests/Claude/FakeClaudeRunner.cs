using Factory.Adapters.Shell;

namespace Factory.Adapters.Tests.Claude;

/// <summary>Records the one claude call and replies with a canned result; makes no real calls.</summary>
internal sealed class FakeClaudeRunner(ProcessResult reply) : ICommandRunner
{
    public string? Command { get; private set; }

    public IReadOnlyList<string>? Args { get; private set; }

    public string? WorkingDirectory { get; private set; }

    public Task<ProcessResult> RunAsync(
        string command, IReadOnlyList<string> args, string? workingDirectory, CancellationToken ct)
    {
        Command = command;
        Args = args;
        WorkingDirectory = workingDirectory;
        return Task.FromResult(reply);
    }
}
