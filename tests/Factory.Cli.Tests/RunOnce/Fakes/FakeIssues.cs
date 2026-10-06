using Factory.Application.Ports;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Cli.Tests.RunOnce.Fakes;

public sealed class FakeIssues(params Issue[] open) : IIssues
{
    public List<IssueNumber> Claimed { get; } = [];

    public Exception? FailWith { get; init; }

    public Task<IReadOnlyList<Issue>> GetOpenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return FailWith is null ? Task.FromResult<IReadOnlyList<Issue>>(open) : Task.FromException<IReadOnlyList<Issue>>(FailWith);
    }

    public Task ClaimAsync(IssueNumber issue, RunId run, CancellationToken ct)
    {
        Claimed.Add(issue);
        return Task.CompletedTask;
    }

    public Task CloseAsync(IssueNumber issue, string comment, CancellationToken ct) => throw new NotSupportedException();

    public Task AddBlockerAsync(IssueNumber blocked, IssueNumber blocker, CancellationToken ct) => throw new NotSupportedException();

    public Task<IssueNumber> CreateAsync(NewIssue issue, CancellationToken ct) => throw new NotSupportedException();
}
