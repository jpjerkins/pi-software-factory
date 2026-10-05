using Factory.Adapters.Shell;

namespace Factory.Adapters.Tests.Shell;

public class ProcessRunnerTests
{
    private static Task<ProcessResult> RunAsync(string command, string[] args, CancellationToken? ct = null) =>
        new ProcessRunner().RunAsync(command, args, workingDirectory: null, ct ?? TestContext.Current.CancellationToken);

    [Fact]
    public async Task Captures_stdout_and_a_zero_exit_code()
    {
        var result = await RunAsync("echo", ["hello", "world"]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello world", result.StdOut.Trim());
    }

    [Fact]
    public async Task Captures_stderr_and_a_non_zero_exit_code()
    {
        var result = await RunAsync("sh", ["-c", "echo oops >&2; exit 3"]);
        Assert.Equal(3, result.ExitCode);
        Assert.Equal("oops", result.StdErr.Trim());
    }

    [Fact]
    public async Task Arguments_are_not_interpreted_by_a_shell()
    {
        var result = await RunAsync("echo", ["$HOME; echo injected"]);
        Assert.Equal("$HOME; echo injected", result.StdOut.Trim());
    }

    [Fact]
    public async Task Cancelling_stops_the_process()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync("sleep", ["30"], cts.Token));
    }
}
