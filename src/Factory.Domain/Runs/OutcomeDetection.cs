namespace Factory.Domain.Runs;

/// <summary>Decides, from what can be observed, whether a worker run has reached an outcome.</summary>
public static class OutcomeDetection
{
    /// <returns>The outcome, or <c>null</c> to keep waiting.</returns>
    public static WorkerOutcome? Detect(
        WorkerResult? result,
        WorkerSignals signals,
        bool isRunning,
        DateTimeOffset now,
        TimeSpan stuckTimeout)
    {
        if (result is not null)
        {
            return result.Status;
        }

        if (signals.SessionEnded || !isRunning)
        {
            return WorkerOutcome.Crashed;
        }

        return now - signals.LastActivity > stuckTimeout ? WorkerOutcome.Stuck : null;
    }
}
