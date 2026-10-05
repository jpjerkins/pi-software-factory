namespace Factory.Domain.Runs;

/// <summary>A started worker, identified by the slot/pane it runs in.</summary>
public sealed record WorkerSession(string Id);
