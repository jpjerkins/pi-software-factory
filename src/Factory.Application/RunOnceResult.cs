using Factory.Domain.Runs;

namespace Factory.Application;

/// <summary>Either a completed run, or nothing to do.</summary>
public sealed record RunOnceResult(Run? Run)
{
    public static RunOnceResult NoWork { get; } = new((Run?)null);

    public bool NothingToDo => Run is null;
}
