using System.Globalization;
using System.Text.RegularExpressions;
using Factory.Adapters.Shell;
using Factory.Application.Ports;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Adapters.GitHub;

/// <summary>Issues in one GitHub repository ("owner/name"), reached through the authenticated <c>gh</c> CLI.</summary>
public sealed partial class GitHubIssues(string repo, ICommandRunner runner) : IIssues
{
    private const string OpenIssuesQuery = """
        query($owner: String!, $name: String!, $cursor: String) {
          repository(owner: $owner, name: $name) {
            issues(states: OPEN, first: 100, after: $cursor, orderBy: {field: CREATED_AT, direction: ASC}) {
              pageInfo { hasNextPage endCursor }
              nodes {
                number title createdAt
                labels(first: 50) { nodes { name } }
                assignees(first: 20) { nodes { login } }
                blockedBy(first: 50) { nodes { number state } }
                blocking(first: 50) { nodes { number state } }
              }
            }
          }
        }
        """;

    public async Task<IReadOnlyList<Issue>> GetOpenAsync(CancellationToken ct)
    {
        var (owner, name) = SplitRepo();
        var issues = new List<Issue>();
        string? cursor = null;
        do
        {
            List<string> args = ["api", "graphql", "-f", $"query={OpenIssuesQuery}", "-f", $"owner={owner}", "-f", $"name={name}"];
            if (cursor is not null)
            {
                args.Add("-f");
                args.Add($"cursor={cursor}");
            }

            var page = OpenIssuesPage.Parse(await GhAsync(args, ct));
            issues.AddRange(page.Issues);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return issues;
    }

    public async Task ClaimAsync(IssueNumber issue, RunId run, CancellationToken ct)
    {
        await GhAsync(["issue", "edit", Num(issue), "-R", repo, "--add-label", EligibilityPolicy.RunningLabel, "--add-assignee", "@me"], ct);
        await GhAsync(["issue", "comment", Num(issue), "-R", repo, "--body", $"Claimed by factory run {run}"], ct);
    }

    public async Task CloseAsync(IssueNumber issue, string comment, CancellationToken ct) =>
        await GhAsync(["issue", "close", Num(issue), "-R", repo, "--comment", comment], ct);

    /// <summary>
    /// GitHub issue dependencies REST API: POST .../issues/{blocked}/dependencies/blocked_by takes the blocker's
    /// numeric database <c>id</c> (not its issue number), so it is fetched first.
    /// </summary>
    public async Task AddBlockerAsync(IssueNumber blocked, IssueNumber blocker, CancellationToken ct)
    {
        var idText = await GhAsync(["api", $"repos/{repo}/issues/{Num(blocker)}", "--jq", ".id"], ct);
        if (!long.TryParse(idText.Trim(), CultureInfo.InvariantCulture, out var id))
        {
            throw new InvalidOperationException($"Could not read the database id of issue {blocker} in {repo}: '{idText.Trim()}'.");
        }

        await GhAsync(
            ["api", "-X", "POST", $"repos/{repo}/issues/{Num(blocked)}/dependencies/blocked_by", "-F", $"issue_id={id}"], ct);
    }

    public async Task<IssueNumber> CreateAsync(NewIssue issue, CancellationToken ct)
    {
        List<string> args = ["issue", "create", "-R", repo, "--title", issue.Title, "--body", issue.Body];
        foreach (var label in issue.Labels)
        {
            args.Add("--label");
            args.Add(label);
        }

        var output = await GhAsync(args, ct);
        var match = IssueUrl().Match(output);
        return match.Success
            ? new IssueNumber(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            : throw new InvalidOperationException($"gh issue create returned no issue URL: '{output.Trim()}'.");
    }

    private static string Num(IssueNumber issue) => issue.Value.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"/issues/(\d+)\s*$")]
    private static partial Regex IssueUrl();

    private (string Owner, string Name) SplitRepo()
    {
        var parts = repo.Split('/');
        return parts.Length == 2 ? (parts[0], parts[1]) : throw new ArgumentException($"Repo must be 'owner/name', got '{repo}'.", nameof(repo));
    }

    private async Task<string> GhAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await runner.RunAsync("gh", args, null, ct);
        return result.ExitCode == 0
            ? result.StdOut
            : throw new GitHubCommandException(string.Join(' ', args.Take(2)), result.ExitCode, result.StdErr);
    }
}
