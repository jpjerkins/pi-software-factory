using System.Text.Json;
using Factory.Domain.Issues;

namespace Factory.Adapters.GitHub;

/// <summary>One page of the open-issues GraphQL response, mapped to Domain issues.</summary>
internal sealed record OpenIssuesPage(IReadOnlyList<Issue> Issues, string? NextCursor)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static OpenIssuesPage Parse(string json)
    {
        var issues = JsonSerializer.Deserialize<Response>(json, Json)!.Data.Repository.Issues;
        var next = issues.PageInfo.HasNextPage ? issues.PageInfo.EndCursor : null;
        return new OpenIssuesPage([.. issues.Nodes.Select(ToIssue)], next);
    }

    private static Issue ToIssue(Node n) => new(
        new IssueNumber(n.Number),
        n.Title,
        IsOpen: true,
        n.CreatedAt,
        [.. n.Labels.Nodes.Select(l => l.Name)],
        [.. n.Assignees.Nodes.Select(a => a.Login)],
        OpenBlockers: [.. n.BlockedBy.Nodes.Where(IsOpen).Select(r => new IssueNumber(r.Number))],
        DirectlyBlocks: [.. n.Blocking.Nodes.Where(IsOpen).Select(r => new IssueNumber(r.Number))],
        Body: n.Body ?? "");

    private static bool IsOpen(Ref r) => r.State.Equals("OPEN", StringComparison.OrdinalIgnoreCase);

    private sealed record Response(DataNode Data);

    private sealed record DataNode(RepositoryNode Repository);

    private sealed record RepositoryNode(IssueConnection Issues);

    private sealed record IssueConnection(PageInfo PageInfo, List<Node> Nodes);

    private sealed record PageInfo(bool HasNextPage, string? EndCursor);

    private sealed record Node(
        int Number, string Title, string? Body, DateTimeOffset CreatedAt,
        Connection<Label> Labels, Connection<Assignee> Assignees, Connection<Ref> BlockedBy, Connection<Ref> Blocking);

    private sealed record Connection<T>(List<T> Nodes);

    private sealed record Label(string Name);

    private sealed record Assignee(string Login);

    private sealed record Ref(int Number, string State);
}
