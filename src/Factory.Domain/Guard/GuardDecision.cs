namespace Factory.Domain.Guard;

/// <summary>Whether a worker's tool call may go ahead. A denial carries the reason shown to the worker.</summary>
public sealed record GuardDecision
{
    public static GuardDecision Allow { get; } = new();

    public bool IsAllowed { get; private init; } = true;

    public string Reason { get; private init; } = "";

    public static GuardDecision Deny(string reason) => new() { IsAllowed = false, Reason = reason };
}
