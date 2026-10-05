using Factory.Adapters.Shell;

namespace Factory.Adapters.Tests.GitHub;

/// <summary>Records every command and replies from a queue of canned results; makes no real calls.</summary>
internal sealed class FakeCommandRunner : ICommandRunner
{
    private readonly Queue<ProcessResult> _replies = new();

    public List<IReadOnlyList<string>> Calls { get; } = [];

    public FakeCommandRunner Replies(string stdOut, int exitCode = 0, string stdErr = "")
    {
        _replies.Enqueue(new ProcessResult(exitCode, stdOut, stdErr));
        return this;
    }

    public Task<ProcessResult> RunAsync(
        string command, IReadOnlyList<string> args, string? workingDirectory, CancellationToken ct)
    {
        Assert.Equal("gh", command);
        Calls.Add(args);
        return Task.FromResult(_replies.Count > 0 ? _replies.Dequeue() : new ProcessResult(0, "", ""));
    }
}
