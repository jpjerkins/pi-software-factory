using Factory.Application.Ports;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Application.Tests.Fakes;

internal sealed class FakeIssues(IEnumerable<Issue> open, CallLog log) : IIssues
{
    public List<(IssueNumber Issue, RunId Run)> Claims { get; } = [];
    public List<(IssueNumber Issue, string Comment)> Closed { get; } = [];
    public List<(IssueNumber Blocked, IssueNumber Blocker)> Blockers { get; } = [];
    public List<NewIssue> Created { get; } = [];

    public Task<IReadOnlyList<Issue>> GetOpenAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Issue>>([.. open]);

    public Task ClaimAsync(IssueNumber issue, RunId run, CancellationToken ct)
    {
        log.Add("claim");
        Claims.Add((issue, run));
        return Task.CompletedTask;
    }

    public Task CloseAsync(IssueNumber issue, string comment, CancellationToken ct)
    {
        Closed.Add((issue, comment));
        return Task.CompletedTask;
    }

    public Task AddBlockerAsync(IssueNumber blocked, IssueNumber blocker, CancellationToken ct)
    {
        Blockers.Add((blocked, blocker));
        return Task.CompletedTask;
    }

    public Task<IssueNumber> CreateAsync(NewIssue issue, CancellationToken ct)
    {
        Created.Add(issue);
        return Task.FromResult(new IssueNumber(1000 + Created.Count));
    }
}
