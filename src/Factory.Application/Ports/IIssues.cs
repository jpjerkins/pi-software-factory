using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Application.Ports;

public interface IIssues
{
    Task<IReadOnlyList<Issue>> GetOpenAsync(CancellationToken ct);

    Task ClaimAsync(IssueNumber issue, RunId run, CancellationToken ct);

    Task CloseAsync(IssueNumber issue, string comment, CancellationToken ct);

    Task AddBlockerAsync(IssueNumber blocked, IssueNumber blocker, CancellationToken ct);

    Task<IssueNumber> CreateAsync(NewIssue issue, CancellationToken ct);
}
