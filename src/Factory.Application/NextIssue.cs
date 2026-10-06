using Factory.Application.Ports;
using Factory.Domain.Issues;

namespace Factory.Application;

/// <summary>Finds the issue the factory would take next, without claiming it.</summary>
public sealed class NextIssue(IIssues issues)
{
    private readonly EligibilityPolicy _eligibility = new();
    private readonly IssueOrdering _ordering = new();

    public async Task<Issue?> FindAsync(CancellationToken ct)
    {
        var open = await issues.GetOpenAsync(ct);
        return _ordering.Order(open.Where(_eligibility.IsEligible)).FirstOrDefault();
    }
}
