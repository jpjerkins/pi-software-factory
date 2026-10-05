namespace Factory.Domain.Runs;

/// <summary>The only statuses a worker may write in result.json.</summary>
public enum ReportedStatus
{
    PlanReady,
    Done,
    NeedsInput,
    Blocked,
}
