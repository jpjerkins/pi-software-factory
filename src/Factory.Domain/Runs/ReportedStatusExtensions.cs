namespace Factory.Domain.Runs;

public static class ReportedStatusExtensions
{
    /// <summary>The factory's verdict for a status the worker reported.</summary>
    public static WorkerOutcome ToOutcome(this ReportedStatus status) => status switch
    {
        ReportedStatus.PlanReady => WorkerOutcome.PlanReady,
        ReportedStatus.Done => WorkerOutcome.Done,
        ReportedStatus.NeedsInput => WorkerOutcome.NeedsInput,
        ReportedStatus.Blocked => WorkerOutcome.Blocked,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
