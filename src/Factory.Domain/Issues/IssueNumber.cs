namespace Factory.Domain.Issues;

/// <summary>The number identifying an issue within its repository.</summary>
public readonly record struct IssueNumber(int Value)
{
    public override string ToString() => $"#{Value}";
}
