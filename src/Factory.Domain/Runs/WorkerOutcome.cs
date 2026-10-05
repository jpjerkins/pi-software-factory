namespace Factory.Domain.Runs;

/// <summary>How a worker session ended up.</summary>
public enum WorkerOutcome
{
    PlanReady,
    Done,
    NeedsInput,
    Blocked,
    Crashed,
    Stuck,
}
