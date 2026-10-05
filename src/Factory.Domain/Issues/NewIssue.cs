namespace Factory.Domain.Issues;

/// <summary>An issue the factory wants created. Factory-logged issues are always handed to Phil, never to an agent.</summary>
public sealed record NewIssue
{
    public const string NeedsPhilLabel = "needs-phil";

    private NewIssue(string title, string body, IReadOnlyList<string> labels)
    {
        Title = title;
        Body = body;
        Labels = labels;
    }

    public string Title { get; }

    public string Body { get; }

    public IReadOnlyList<string> Labels { get; }

    /// <summary>An issue logged by the factory: labelled exactly <c>needs-phil</c>, never <c>build</c> or <c>agent:*</c>.</summary>
    public static NewIssue Logged(string title, string body) => new(title, body, [NeedsPhilLabel]);
}
