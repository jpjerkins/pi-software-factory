using System.Diagnostics;

namespace Factory.Adapters.Shell;

/// <summary>Runs a command with an argument list (never a shell string) and captures its output.</summary>
public sealed class ProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string command, IReadOnlyList<string> args, string? workingDirectory, CancellationToken ct)
    {
        var info = new ProcessStartInfo(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        if (workingDirectory is not null)
        {
            info.WorkingDirectory = workingDirectory;
        }

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start '{command}'.");
        process.StandardInput.Close();

        var stdOut = process.StandardOutput.ReadToEndAsync(ct);
        var stdErr = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct);
            return new ProcessResult(process.ExitCode, await stdOut, await stdErr);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
