namespace Factory.Domain.Runs;

/// <summary>What the worker reported in result.json.</summary>
public sealed record WorkerResult(
    ReportedStatus Status,
    string Summary,
    IReadOnlyList<string> TestsRun,
    IReadOnlyList<string> FilesTouched,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> ProblemsFound);
