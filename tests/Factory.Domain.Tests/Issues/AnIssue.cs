using Factory.Domain.Issues;

namespace Factory.Domain.Tests.Issues;

/// <summary>Test data builder: defaults to an issue that is eligible.</summary>
internal sealed class AnIssue
{
    private int _number = 1;
    private string _title = "An issue";
    private bool _isOpen = true;
    private DateTimeOffset _createdAt = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private List<string> _labels = ["build", "agent:claude", "lane:adapters"];
    private List<string> _assignees = [];
    private List<IssueNumber> _openBlockers = [];
    private List<IssueNumber> _directlyBlocks = [];

    public static AnIssue Eligible() => new();

    public AnIssue Number(int number) { _number = number; return this; }
    public AnIssue Titled(string title) { _title = title; return this; }
    public AnIssue Closed() { _isOpen = false; return this; }
    public AnIssue CreatedAt(DateTimeOffset at) { _createdAt = at; return this; }
    public AnIssue Labelled(params string[] labels) { _labels = [.. labels]; return this; }
    public AnIssue WithLabel(string label) { _labels.Add(label); return this; }
    public AnIssue WithoutLabel(string label) { _labels.Remove(label); return this; }
    public AnIssue AssignedTo(string login) { _assignees.Add(login); return this; }
    public AnIssue BlockedByOpen(int number) { _openBlockers.Add(new IssueNumber(number)); return this; }
    public AnIssue Blocking(params int[] numbers) { _directlyBlocks = [.. numbers.Select(n => new IssueNumber(n))]; return this; }

    public Issue Build() =>
        new(new IssueNumber(_number), _title, _isOpen, _createdAt, _labels, _assignees, _openBlockers, _directlyBlocks);
}
