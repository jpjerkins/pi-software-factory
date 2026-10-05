using System.Text.Json;
using Factory.Domain.Issues;

namespace Factory.Domain.Tests.Issues;

/// <summary>
/// Loads a captured snapshot of open issues (gh api graphql: blockedBy / blocking) and maps it to the Domain.
/// File shape: { capturedOn, repository, issues: [ { number, title, createdAt, labels[], assignees[],
/// blockedBy: [ { number, state } ], blocking: [ { number, state } ] } ] }. All issues in the file are open.
/// </summary>
internal static class TaskGuideIssueFixture
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<Issue> Load(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path), Json)!;
        return [.. snapshot.Issues.Select(ToIssue)];
    }

    private static Issue ToIssue(IssueDto dto) => new(
        new IssueNumber(dto.Number),
        dto.Title,
        IsOpen: true,
        dto.CreatedAt,
        dto.Labels,
        dto.Assignees,
        OpenBlockers: [.. dto.BlockedBy.Where(IsOpen).Select(r => new IssueNumber(r.Number))],
        DirectlyBlocks: [.. dto.Blocking.Where(IsOpen).Select(r => new IssueNumber(r.Number))]);

    private static bool IsOpen(RefDto r) => r.State.Equals("OPEN", StringComparison.OrdinalIgnoreCase);

    private sealed record Snapshot(List<IssueDto> Issues);

    private sealed record IssueDto(
        int Number, string Title, DateTimeOffset CreatedAt,
        List<string> Labels, List<string> Assignees, List<RefDto> BlockedBy, List<RefDto> Blocking);

    private sealed record RefDto(int Number, string State);
}
