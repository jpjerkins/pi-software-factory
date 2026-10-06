namespace Factory.Domain.Runs;

/// <summary>A started worker: the pane it runs in, its agent name, and the Claude session id (kept so a run can be resumed).</summary>
public sealed record WorkerSession(string PaneId, string AgentName, string SessionId);
