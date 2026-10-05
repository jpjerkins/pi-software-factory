using Factory.Domain.Runs;

namespace Factory.Adapters.Storage;

/// <summary>The shape of result.json as the worker writes it. Status stays a string so it can be checked strictly.</summary>
internal sealed record ResultDocument(
    string? Status,
    string? Summary,
    List<string>? TestsRun,
    List<string>? FilesTouched,
    List<string>? Questions,
    List<string>? ProblemsFound)
{
    /// <returns>The result, or <c>null</c> if the status is not one a worker may report.</returns>
    public WorkerResult? ToResult()
    {
        ReportedStatus? status = Status switch
        {
            "plan_ready" => ReportedStatus.PlanReady,
            "done" => ReportedStatus.Done,
            "needs_input" => ReportedStatus.NeedsInput,
            "blocked" => ReportedStatus.Blocked,
            _ => null,
        };

        return status is { } s
            ? new WorkerResult(s, Summary ?? "", TestsRun ?? [], FilesTouched ?? [], Questions ?? [], ProblemsFound ?? [])
            : null;
    }
}
