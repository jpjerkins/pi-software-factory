namespace Factory.Adapters.Shell;

/// <summary>Runs a command with an argument list (never a shell string); a seam so adapters can be tested without live calls.</summary>
public interface ICommandRunner
{
    Task<ProcessResult> RunAsync(string command, IReadOnlyList<string> args, string? workingDirectory, CancellationToken ct);
}
