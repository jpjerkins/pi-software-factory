namespace Factory.Domain.Runs;

/// <summary>What the hooks recorded about a worker: when it was last active and whether its session ended.</summary>
public sealed record WorkerSignals(DateTimeOffset LastActivity, bool SessionEnded);
